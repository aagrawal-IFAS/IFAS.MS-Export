namespace IFAS.MS.Job
{
    public class MSSettingOptions
    {
        public int CompanyId { get; set; }
        public int BatchSize { get; set; }
        public int ExportIntervalInMinutes {  get; set; }
        public int ExportHandshakeIntervalInMinutes { get; set; }         
    }
}
