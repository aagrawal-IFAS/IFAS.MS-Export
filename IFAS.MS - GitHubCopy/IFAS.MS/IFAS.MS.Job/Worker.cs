using Hangfire;
using Microsoft.Extensions.Options;

namespace IFAS.MS.Job
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly MSSettingOptions _settings;

        public Worker(ILogger<Worker> logger, IBackgroundJobClient backgroundJobClient, IOptions<MSSettingOptions> msSettings)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _settings = msSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            try
            {
                RecurringJob.AddOrUpdate<IFASMSJobService>(
                     "my-recurring-exportDataTask",
                     job => job.ExecuteExportTaskAsync($"Recurring job {Guid.NewGuid()}"),
                     $"*/{_settings.ExportIntervalInMinutes} * * * *");

                _logger.LogInformation("Recurring API call job 'my-recurring-exportDataTask' added or updated.");

                RecurringJob.AddOrUpdate<IFASMSJobService>(
                     "my-recurring-exportDataHandshakeTask",
                     job => job.ExecuteExportHandshakeTaskAsync($"Recurring job {Guid.NewGuid()}"),
                     $"*/{_settings.ExportHandshakeIntervalInMinutes} * * * *");

                _logger.LogInformation("Recurring API call job 'my-recurring-exportDataHandshakeTask' added or updated.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Worker heartbeat at: {time}", DateTimeOffset.Now);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {               
                _logger.LogInformation("Worker service stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred in the Worker service execution loop.");               
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker service gracefully stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
