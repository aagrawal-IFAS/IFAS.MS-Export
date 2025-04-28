using Dapper;
using IFAS.MS.Models;
using IFAS.MS.Synchronization.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace IFAS.MS.Synchronization
{
    public class ExportService : IExportService
    {
        private readonly IDbConnection _dbConnection;

        public ExportService(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        }

        public async Task<IEnumerable<SSReplicationData>> GetExportData(int companyId, int batchSize)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@pCompanyId", companyId, DbType.Int32);
                parameters.Add("@pBatchSize", batchSize, DbType.Int32);

                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();

                var exportableData = await _dbConnection.QueryAsync<SSReplicationData>(
                    "dbo.usp_MS_Sync_GetExportData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return exportableData;
            }
            catch (SqlException)
            {
                return Enumerable.Empty<SSReplicationData>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<SSReplicationData>();
            }
        }

        public async Task<bool> SaveExportData(string exportDataJson)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@pExportedData", exportDataJson, DbType.String);

                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();

                await _dbConnection.ExecuteAsync(
                    "dbo.usp_MS_Sync_SaveExportedData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return true;
            }
            catch (SqlException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally { _dbConnection.Close(); }
        }

        public async Task<IEnumerable<SSReplicationDataHandshake>> CheckExportHandshake(string ReplicationHandshakeData)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@pExportHandShakeData", ReplicationHandshakeData, DbType.String);

                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();

                var exportableData = await _dbConnection.QueryAsync<SSReplicationDataHandshake>(
                    "dbo.usp_MS_Sync_GetCheckHandshakeData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return exportableData;
            }
            catch (SqlException)
            {
                return Enumerable.Empty<SSReplicationDataHandshake>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<SSReplicationDataHandshake>();
            }
        }

        public async Task<IEnumerable<SSReplicationDataHandshake>> GetExportDataHandshake(int companyId, int batchSize)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@pCompanyId", companyId, DbType.Int32);
                parameters.Add("@pBatchSize", batchSize, DbType.Int32);

                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();

                var exportableData = await _dbConnection.QueryAsync<SSReplicationDataHandshake>(
                    "dbo.usp_MS_Sync_GetExportHandshakeData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return exportableData;
            }
            catch (SqlException)
            {
                return Enumerable.Empty<SSReplicationDataHandshake>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<SSReplicationDataHandshake>();
            }
        }

        public async Task<bool> UpdateExportHandshakeData(string exportDataJson)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@pExportHandShakeData", exportDataJson, DbType.String);

                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();

                await _dbConnection.QueryAsync<SSReplicationDataHandshake>(
                    "dbo.usp_MS_Sync_UpdateCheckHandshakeData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return true;
            }
            catch (SqlException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
