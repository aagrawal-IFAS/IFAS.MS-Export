namespace IFAS.MS.AdaptiveTransmissionAnalyzer
{
    /// <summary>
    /// Holds information about a file used for speed testing.
    /// </summary>
    public class TestFileInfo
    {
        public string Url { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }
}
