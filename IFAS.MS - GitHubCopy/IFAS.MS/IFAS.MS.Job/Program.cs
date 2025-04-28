using IFAS.MS.Job;
using IFAS.MS.AdaptiveTransmissionAnalyzer;
using Hangfire;
using Hangfire.SqlServer;
using IFAS.MS.Job.Interfaces;
using IFAS.MS.Synchronization.Interfaces;
using IFAS.MS.Synchronization;
using Microsoft.Data.SqlClient;
using System.Data;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((hostContext, services) => {
    IConfiguration configuration = hostContext.Configuration;

    services.Configure<MicroserviceApiOptions>(configuration.GetSection("MicroserviceApi"));
    services.Configure<MSSettingOptions>(configuration.GetSection("MSSettings"));

    var apiOptions = configuration.GetSection("MicroserviceApi").Get<MicroserviceApiOptions>();

    if (string.IsNullOrEmpty(apiOptions?.BaseUrl) || !Uri.IsWellFormedUriString(apiOptions.BaseUrl, UriKind.Absolute))
    {
        throw new InvalidOperationException("Microservice API BaseUrl is missing or invalid in configuration.");
    }

    services.AddHttpClient("MicroserviceApiClient", client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    });

    services.AddTransient<IDbConnection>((sp) => new SqlConnection(configuration.GetConnectionString("IFASConnection")));
    services.AddTransient<IExportService, ExportService>();
    services.AddTransient<IIFASMSJobService, IFASMSJobService>();

    services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

    services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
        options.Queues = new[] { "default", "critical" };
        options.ServerName = $"{Environment.MachineName}.Worker.1";
    });

    services.AddHostedService<Worker>();
});

AdaptiveTransmissionAnalyzer.Analyze().GetAwaiter();

var host = builder.Build();
host.Run();
