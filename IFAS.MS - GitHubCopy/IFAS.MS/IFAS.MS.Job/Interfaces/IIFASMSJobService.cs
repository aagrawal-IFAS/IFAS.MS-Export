namespace IFAS.MS.Job.Interfaces
{
    public interface IIFASMSJobService
    {
        Task ExecuteExportTaskAsync(string taskId);
        Task ExecuteExportHandshakeTaskAsync(string taskId);
    }
}
