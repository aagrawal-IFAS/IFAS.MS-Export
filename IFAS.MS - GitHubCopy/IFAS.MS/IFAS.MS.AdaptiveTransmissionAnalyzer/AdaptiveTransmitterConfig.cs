namespace IFAS.MS.AdaptiveTransmissionAnalyzer
{
    /// <summary>
    /// Configuration for the EnhancedAdaptiveTransmitter.
    /// </summary>
    public class AdaptiveTransmitterConfig
    {
        // --- Core Parameters ---
        public double TargetTransmissionSeconds { get; set; } = 5.0;
        public int MinObjectCount { get; set; } = 1;
        public int MaxObjectCount { get; set; } = 100;
        public int? InitialObjectCount { get; set; } = null;

        // --- Adaptation Tuning ---
        public double BaseDecreaseFactor { get; set; } = 0.90;
        public double BaseIncreaseStep { get; set; } = 1.0;
        public double SuccessRateThreshold { get; set; } = 0.98;
        public double DurationOverTargetThresholdFactor { get; set; } = 1.2;
        public double DurationUnderTargetThresholdFactor { get; set; } = 0.7;

        // --- Smoothing Factors (Alpha for EMA) ---
        public double SpeedSmoothingFactor { get; set; } = 0.2;
        public double DurationSmoothingFactor { get; set; } = 0.3;

        // --- Speed Test Parameters ---
        public TestFileInfo[]? SpeedTestFiles { get; set; }
        public int SpeedTestTimeoutSeconds { get; set; } = 30;
    }    
}
