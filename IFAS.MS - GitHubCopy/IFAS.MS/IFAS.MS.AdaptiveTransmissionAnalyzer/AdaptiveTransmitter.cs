using System.Diagnostics;

namespace IFAS.MS.AdaptiveTransmissionAnalyzer
{
    public class AdaptiveTransmitter
    {
        // Configuration
        private readonly AdaptiveTransmitterConfig _config;
        private readonly double _bytesToMegabitsFactor = 8.0 / (1024 * 1024);
        private readonly HttpClient _httpClient;

        // State
        private int _currentObjectCount;
        private double _emaDownloadSpeedMbps = -1;
        private double _emaDurationPerObjectSec = -1;
        private int _speedTestFileIndex = 0;
        private readonly object _lock = new object(); // For thread safety

        /// <summary>
        /// Initializes the EnhancedAdaptiveTransmitter.
        /// </summary>
        public AdaptiveTransmitter(AdaptiveTransmitterConfig config, HttpClient httpClient)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Validate config ranges
            _config.MinObjectCount = Math.Max(1, _config.MinObjectCount);
            _config.MaxObjectCount = Math.Max(_config.MinObjectCount + 1, _config.MaxObjectCount);
            _config.BaseDecreaseFactor = Math.Clamp(_config.BaseDecreaseFactor, 0.1, 0.99);
            _config.BaseIncreaseStep = Math.Max(1, _config.BaseIncreaseStep);
            _config.SuccessRateThreshold = Math.Clamp(_config.SuccessRateThreshold, 0.5, 1.0);
            _config.DurationOverTargetThresholdFactor = Math.Max(1.0, _config.DurationOverTargetThresholdFactor);
            _config.DurationUnderTargetThresholdFactor = Math.Clamp(_config.DurationUnderTargetThresholdFactor, 0.1, 1.0);
            _config.SpeedSmoothingFactor = Math.Clamp(_config.SpeedSmoothingFactor, 0.01, 1.0);
            _config.DurationSmoothingFactor = Math.Clamp(_config.DurationSmoothingFactor, 0.01, 1.0);

            int initialCount = _config.InitialObjectCount.HasValue
                ? Math.Clamp(_config.InitialObjectCount.Value, _config.MinObjectCount, _config.MaxObjectCount)
                : Math.Clamp(_config.MaxObjectCount / 2, _config.MinObjectCount, _config.MaxObjectCount);

            _currentObjectCount = initialCount;            
        }

        /// <summary>
        /// Gets the current recommended number of objects to transmit.
        /// </summary>
        public int GetCurrentObjectCount()
        {
            lock (_lock) { return _emaDownloadSpeedMbps > 0 ?  _currentObjectCount : 0; }
        }

        /// <summary>
        /// Gets the exponentially smoothed moving average of the download speed in Mbps.
        /// </summary>
        public double EmaDownloadSpeedMbps
        {
            get { lock (_lock) { return _emaDownloadSpeedMbps; } }
        }

        /// <summary>
        /// Gets the exponentially smoothed moving average of the transmission duration per object in seconds.
        /// </summary>
        public double EmaDurationPerObjectSec
        {
            get { lock (_lock) { return _emaDurationPerObjectSec; } }
        }

        /// <summary>
        /// Estimates download speed by downloading a test file and updates the EMA speed.
        /// </summary>
        public async Task<double> MeasureDownloadSpeedMbpsAsync(CancellationToken cancellationToken = default)
        {
            if (_config.SpeedTestFiles == null || _config.SpeedTestFiles.Length == 0)
            {
                return -1;
            }

            TestFileInfo testFile;
            lock (_lock)
            {
                _speedTestFileIndex = (_speedTestFileIndex + 1) % _config.SpeedTestFiles.Length;
                if (_speedTestFileIndex < 0 || _speedTestFileIndex >= _config.SpeedTestFiles.Length)
                {
                    return -1; // Prevent index out of bounds
                }
                testFile = _config.SpeedTestFiles[_speedTestFileIndex];
            }

            if (string.IsNullOrEmpty(testFile.Url) || testFile.SizeBytes <= 0)
            {
                return -1;
            }

            var stopwatch = Stopwatch.StartNew();
            double measuredSpeedMbps = -1;
            long totalBytesRead = 0;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_config.SpeedTestTimeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Get, testFile.Url);
                // Ensure redirects are followed if necessary (default is usually true)
                // request.Properties["AllowAutoRedirect"] = true;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode(); // Throws on non-2xx codes

                var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                byte[] buffer = new byte[8192]; // 8KB buffer
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                {
                    totalBytesRead += bytesRead;
                }

                stopwatch.Stop();
                double seconds = stopwatch.Elapsed.TotalSeconds;

                if (seconds > 0.001 && totalBytesRead > 0) // Avoid division by zero or near-zero
                {
                    measuredSpeedMbps = (totalBytesRead * _bytesToMegabitsFactor) / seconds;

                    lock (_lock)
                    {
                        if (_emaDownloadSpeedMbps < 0)
                        {
                            _emaDownloadSpeedMbps = measuredSpeedMbps;
                        }
                        else
                        {
                            _emaDownloadSpeedMbps = (measuredSpeedMbps * _config.SpeedSmoothingFactor) + (_emaDownloadSpeedMbps * (1.0 - _config.SpeedSmoothingFactor));
                        }
                    }
                }
                else
                {
                    measuredSpeedMbps = -1;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                measuredSpeedMbps = -1;
            }
            catch (OperationCanceledException) // Catches timeout
            {
                stopwatch.Stop();
                measuredSpeedMbps = -1;
            }
            catch (HttpRequestException)
            {
                stopwatch.Stop();
                measuredSpeedMbps = -1;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                measuredSpeedMbps = -1;
            }

            if (measuredSpeedMbps < 0)
                _emaDownloadSpeedMbps = 0;

            return measuredSpeedMbps;
        }

        /// <summary>
        /// Records the result of a transmission attempt and adjusts the object count.
        /// </summary>
        public void RecordTransmissionResult(TimeSpan duration, int objectsAttempted, int objectsSucceeded)
        {
            if (objectsAttempted <= 0)
            {
                return;
            }

            double successRate = (double)objectsSucceeded / objectsAttempted;
            double durationSeconds = duration.TotalSeconds;
            // Calculate duration per *successful* object to avoid rewarding failures with faster times
            double currentDurationPerObject = (objectsSucceeded > 0 && durationSeconds > 0)
                                              ? durationSeconds / objectsSucceeded
                                              : double.MaxValue;

            lock (_lock)
            {
                int previousCount = _currentObjectCount;

                // Update EMA Duration only if the calculation is valid
                if (successRate > 0 && currentDurationPerObject != double.MaxValue)
                {
                    if (_emaDurationPerObjectSec < 0)
                    {
                        _emaDurationPerObjectSec = currentDurationPerObject;
                    }
                    else
                    {
                        _emaDurationPerObjectSec = (currentDurationPerObject * _config.DurationSmoothingFactor) + (_emaDurationPerObjectSec * (1.0 - _config.DurationSmoothingFactor));
                    }
                }

                // --- Adaptive Logic ---
                if (_emaDurationPerObjectSec > 0) // Only adapt if we have a valid EMA duration
                {
                    // Estimate duration based on current count and *smoothed* time per object
                    double estimatedTotalDuration = _emaDurationPerObjectSec * _currentObjectCount;
                    double durationRatio = estimatedTotalDuration / _config.TargetTransmissionSeconds;

                    bool decrease = false;
                    double adjustmentFactor = 1.0; // Scales the magnitude of change

                    // Decrease Condition 1: Success rate too low
                    if (successRate < _config.SuccessRateThreshold)
                    {
                        decrease = true;
                        // Make penalty harsher the further below threshold we are
                        adjustmentFactor = Math.Max(1.0, 1.0 + (1.0 - (successRate / _config.SuccessRateThreshold)) * 1.5); // Example scaling
                    }
                    // Decrease Condition 2: Estimated duration too long (only if success rate was okay)
                    else if (durationRatio > _config.DurationOverTargetThresholdFactor)
                    {
                        decrease = true;
                        // Scale penalty by how much over target
                        adjustmentFactor = Math.Max(1.0, durationRatio / _config.DurationOverTargetThresholdFactor);
                    }

                    if (decrease)
                    {
                        double decreaseAmount = _currentObjectCount * (1.0 - _config.BaseDecreaseFactor);
                        int actualDecrease = (int)Math.Max(1, Math.Ceiling(decreaseAmount * adjustmentFactor)); // Decrease by at least 1
                        _currentObjectCount = Math.Max(_config.MinObjectCount, _currentObjectCount - actualDecrease);
                    }
                    else // Increase only if success is high AND duration is well below target
                    {
                        if (durationRatio < _config.DurationUnderTargetThresholdFactor && successRate >= _config.SuccessRateThreshold) // Use threshold here too
                        {
                            // Scale increase reward by how much under target
                            adjustmentFactor = Math.Max(1.0, (_config.DurationUnderTargetThresholdFactor / durationRatio) * 1.2); // Example scaling
                            int actualIncrease = (int)Math.Max(1, Math.Ceiling(_config.BaseIncreaseStep * adjustmentFactor)); // Increase by at least 1
                            _currentObjectCount = Math.Min(_config.MaxObjectCount, _currentObjectCount + actualIncrease);
                        }
                    }
                }
            } // End lock
        }
    }
}
