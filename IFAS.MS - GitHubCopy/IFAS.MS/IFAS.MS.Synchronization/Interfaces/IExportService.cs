using IFAS.MS.Models;

namespace IFAS.MS.Synchronization.Interfaces
{
    public interface IExportService
    {
        Task<IEnumerable<SSReplicationData>> GetExportData(int companyId, int batchSize);
        Task<IEnumerable<SSReplicationDataHandshake>> CheckExportHandshake(string ReplicationHandshakeData);
        Task<bool> SaveExportData(string exportDataJson);
        Task<IEnumerable<SSReplicationDataHandshake>> GetExportDataHandshake(int companyId, int batchSize);
        Task<bool> UpdateExportHandshakeData(string exportDataJson);

    }
}
