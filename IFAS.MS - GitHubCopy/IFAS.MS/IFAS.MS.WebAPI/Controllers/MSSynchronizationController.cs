using IFAS.MS.Models;
using IFAS.MS.Synchronization.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections;

namespace IFAS.MS.WebAPI.Controllers
{
    [ApiController]
    public class MSSynchronizationController : ControllerBase
    {
        private readonly IExportService _exportService;
        public MSSynchronizationController(IExportService exportService)
        {
            _exportService = exportService;
        }

        [HttpGet, Route("api/MSSynchronization/IsConnectionOpen")]
        public bool IsConnectionOpen()
        {
            return true;
        }

        [HttpPost, Route("api/MSSynchronization/ExportData")]
        public bool SaveExportData(IEnumerable<SSReplicationData> replicationData)
        {
            try
            {
                if(replicationData?.Any() ?? false)
                {
                    var result = _exportService.SaveExportData(JsonConvert.SerializeObject(replicationData)).GetAwaiter().GetResult();
                    return result;
                }
               
                return false;
            }
            catch (Exception) { return false; }           
        }

        [HttpPost, Route("api/MSSynchronization/GetExportedDataStatus")]
        public IEnumerable<SSReplicationDataHandshake> GetExportedDataStatus(IEnumerable<SSReplicationData> replicationData)
        {
            try
            {
                if (replicationData?.Any() ?? false)
                {
                    var result = _exportService.CheckExportHandshake(JsonConvert.SerializeObject(replicationData)).GetAwaiter().GetResult();
                    return result;
                }

                return Enumerable.Empty<SSReplicationDataHandshake>();
            }
            catch (Exception) { return Enumerable.Empty<SSReplicationDataHandshake>(); }
        }
    }
}
