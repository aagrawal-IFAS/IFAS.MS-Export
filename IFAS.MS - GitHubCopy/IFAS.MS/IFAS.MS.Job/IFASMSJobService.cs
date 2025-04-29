using Hangfire;
using IFAS.MS.Job.Interfaces;
using IFAS.MS.Models;
using IFAS.MS.Synchronization.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;

namespace IFAS.MS.Job
{
    public class IFASMSJobService : IIFASMSJobService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IFASMSJobService> _logger;
        private readonly IExportService _exportService;
        private readonly string serviceUrl;
        private readonly MSSettingOptions _settings;

        public IFASMSJobService(IHttpClientFactory httpClientFactory, ILogger<IFASMSJobService> logger, IExportService exportService, IOptions<MicroserviceApiOptions> apiOptions, IOptions<MSSettingOptions> msSettings)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            serviceUrl = apiOptions.Value.BaseUrl ?? throw new ArgumentNullException(nameof(apiOptions));
            _settings = msSettings.Value ?? throw new ArgumentNullException(nameof(msSettings));
        }

        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new int[] { 60, 120, 300 })]
        public async Task ExecuteExportTaskAsync(string taskId)
        {
            try
            {
                if(AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentObjectCount > 0 && AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentSpeedInMbps > 0)
                {
                    if (IsConnectionOpen(serviceUrl).GetAwaiter().GetResult()) 
                    {
                        var exportableData = _exportService.GetExportData(_settings.CompanyId, AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentObjectCount).GetAwaiter().GetResult();
                        var client = _httpClientFactory.CreateClient();

                        using var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(exportableData),null, "application/json");
                        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serviceUrl}\\Exportdata") { Content = content };

                        var response = await client.SendAsync(request);                       

                        if (response.IsSuccessStatusCode == false)                       
                        {
                            response.EnsureSuccessStatusCode();
                        }
                    }                    
                }               
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }            
        }

        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new int[] { 60, 120, 300 })]
        public async Task ExecuteExportHandshakeTaskAsync(string taskId)
        {
            try
            {
                if (AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentObjectCount > 0 && AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentSpeedInMbps > 0)
                {
                    if (IsConnectionOpen(serviceUrl).GetAwaiter().GetResult())
                    {
                        var exportableData = _exportService.GetExportDataHandshake(_settings.CompanyId, AdaptiveTransmissionAnalyzer.AdaptiveTransmissionAnalyzer.CurrentObjectCount).GetAwaiter().GetResult();
                        var client = _httpClientFactory.CreateClient();

                        using var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(exportableData), Encoding.UTF8, "application/json");
                        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serviceUrl}\\GetExportedDataStatus") { Content = content };

                        var response = await client.SendAsync(request);

                        if(response.IsSuccessStatusCode)
                        {
                            var responseData = await response.Content.ReadAsStringAsync();

                            var exportedDataStatus = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<SSReplicationDataHandshake>>(responseData);

                            if (exportedDataStatus?.Any() ?? false)
                            {
                                await _exportService.UpdateExportHandshakeData(responseData);
                            }
                        }
                        else
                        {
                            response.EnsureSuccessStatusCode();
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task<bool> IsConnectionOpen(string serviceUrl)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{serviceUrl}\\IsConnectionOpen") { Content = null };

                var response = await client.SendAsync(request);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (HttpRequestException)
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
