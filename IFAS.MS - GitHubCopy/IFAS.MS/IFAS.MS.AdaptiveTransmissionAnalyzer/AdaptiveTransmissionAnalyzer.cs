namespace IFAS.MS.AdaptiveTransmissionAnalyzer
{
    public class AdaptiveTransmissionAnalyzer
    {
        private static readonly HttpClient staticHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static AdaptiveTransmitterConfig config = new AdaptiveTransmitterConfig();


        private static AdaptiveTransmitter transmitter = new AdaptiveTransmitter(config, staticHttpClient);
        
        // --- Simulation Helper ---
        private static Random _random = new Random();       

        /// <summary>
        /// Simulates transmitting objects, returning duration and success count.
        /// Influenced by object count and estimated network speed.
        /// </summary>
        private static void SimulateTransmission(int objectCount, double currentEmaSpeedMbps, out TimeSpan duration, out int succeededCount)
        {
            // Simulate base time per object - inversely related to speed (very rough!)
            // Clamp speed effect to avoid extreme values
            double speedFactor = Math.Clamp(currentEmaSpeedMbps > 1 ? 10.0 / currentEmaSpeedMbps : 10.0, 0.3, 10.0); // Lower speed = higher factor
            double baseSecondsPerObject = (0.02 + (_random.NextDouble() * 0.06)) * speedFactor; // Base 20-80ms, scaled by speed estimate

            // Add variability/noise simulating network jitter
            double networkNoiseFactor = 1.0 + (_random.NextDouble() * 0.5 - 0.25); // +/- 25% noise
            double simulatedSeconds = objectCount * baseSecondsPerObject * networkNoiseFactor;
            simulatedSeconds = Math.Max(0.1, simulatedSeconds); // Ensure minimum realistic time

            duration = TimeSpan.FromSeconds(simulatedSeconds);

            // Simulate failures - more likely if duration is long (implies congestion), or randomly
            // Higher chance if simulated time per object is high
            double timePerObjectFactor = Math.Clamp(simulatedSeconds / objectCount, 0, 0.5); // Factor based on time per object (max 0.5)
            double failureChance = 0.01 + timePerObjectFactor * 0.2 + (_random.NextDouble() * 0.05); // Base 1% + up to 10% based on time + 5% random
            failureChance = Math.Clamp(failureChance, 0, 0.15); // Cap failure chance at 15%

            if (_random.NextDouble() < failureChance)
            {
                // If failure occurs, severity is random
                double successReduction = _random.NextDouble() * 0.6; // Reduce success by up to 60%
                succeededCount = (int)Math.Floor(objectCount * (1.0 - successReduction));
                succeededCount = Math.Clamp(succeededCount, 0, objectCount);
            }
            else
            {
                succeededCount = objectCount; // All succeeded
            }
        }

        public static bool CheckContinousInternetSpeed {  get; set; } = true;

        public static int CurrentObjectCount => transmitter.GetCurrentObjectCount();

        public static double CurrentSpeedInMbps => transmitter.EmaDownloadSpeedMbps;

        public static async Task Analyze()
        {
            config = new AdaptiveTransmitterConfig
            {
                TargetTransmissionSeconds = 4.0, // Aim for batches to take 4 seconds
                MinObjectCount = 5,         // Never send fewer than 5 objects
                MaxObjectCount = 150,       // Never send more than 150 objects
                InitialObjectCount = 20,    // Start by trying 20 objects
                BaseIncreaseStep = 2,       // Increase by 2 objects (base) on success/speed
                BaseDecreaseFactor = 0.85,  // Decrease to 85% of current count (base) on failure/slowness
                SuccessRateThreshold = 0.95,// Trigger decrease if less than 95% success
                DurationOverTargetThresholdFactor = 1.3, // Trigger decrease if estimated time is > 30% over target
                DurationUnderTargetThresholdFactor = 0.6, // Allow increase if estimated time is < 60% of target (and success is high)
                SpeedSmoothingFactor = 0.25, // EMA alpha for speed (more weight on recent tests)
                DurationSmoothingFactor = 0.35, // EMA alpha for duration (more weight on recent transmissions)
                SpeedTestFiles = new TestFileInfo[] {
                 // Consider hosting your own test files.
                new TestFileInfo { Url = "https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_1MB_JPG.jpg", SizeBytes = 1024*1024 }, // 10 * 1024 * 1024
                new TestFileInfo { Url = "https://freetestdata.com/wp-content/uploads/2021/09/Free_Test_Data_2MB_MP3.mp3", SizeBytes = 2*1024*1024 },   // 5 * 1024 * 1024               
            },
                SpeedTestTimeoutSeconds = 20 // Timeout for speed test downloads
            };

            transmitter = new AdaptiveTransmitter(config, staticHttpClient);            

            while (CheckContinousInternetSpeed)
            {
                await transmitter.MeasureDownloadSpeedMbpsAsync();

                int objectsToSend = transmitter.GetCurrentObjectCount();

                if(transmitter.EmaDownloadSpeedMbps > 0)
                {
                    SimulateTransmission(objectsToSend, transmitter.EmaDownloadSpeedMbps, out TimeSpan actualDuration, out int succeededCount);

                    transmitter.RecordTransmissionResult(actualDuration, objectsToSend, succeededCount);

                    Task.Delay(1500).GetAwaiter();
                }               
            }
        }        
    }
}
