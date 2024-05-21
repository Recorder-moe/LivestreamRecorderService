using System.Configuration;
using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.SingletonServices;
using LivestreamRecorderService.SingletonServices.Downloader;
using LivestreamRecorderService.Workers;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

#if !RELEASE
SelfLog.Enable(Console.WriteLine);
#endif


IConfiguration configuration = new ConfigurationBuilder()
                               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#if !RELEASE
                               .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                               .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
#endif
                               .AddEnvironmentVariables()
                               .Build();

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration)
                                      .Enrich.WithMachineName()
                                      .Enrich.FromLogContext()
                                      .CreateLogger();

Log.Information("Starting up...");

try
{
    IHostBuilder hostBuilder =
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                configuration = context.Configuration;
                services.AddHttpClient();

                services.AddOptions<AzureOption>()
                        .Bind(configuration.GetSection(AzureOption.ConfigurationSectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                services.AddOptions<S3Option>()
                        .Bind(configuration.GetSection(S3Option.ConfigurationSectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                services.AddOptions<ServiceOption>()
                        .Bind(configuration.GetSection(ServiceOption.ConfigurationSectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                services.AddOptions<CouchDbOption>()
                        .Bind(configuration.GetSection(CouchDbOption.ConfigurationSectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                ServiceOption serviceOptions = services.BuildServiceProvider()
                                                       .GetRequiredService<IOptions<ServiceOption>>()
                                                       .Value;

                switch (serviceOptions.JobService)
                {
                    case ServiceName.AzureContainerInstance:
                        services.AddAzureContainerInstanceService();
                        break;
                    case ServiceName.Kubernetes:
                        services.AddKubernetesService(configuration);
                        break;
                    default:
                        Log.Fatal("Job Service is limited to Azure Container Instance, Kubernetes.");
                        throw new ConfigurationErrorsException(
                            "Job Service is limited to Azure Container Instance, Kubernetes.");
                }

                switch (serviceOptions.StorageService)
                {
                    case ServiceName.AzureBlobStorage:
                        services.AddAzureBlobStorageService();
                        break;
                    case ServiceName.S3:
                        services.AddS3StorageService();
                        break;
                    default:
                        Log.Fatal("Storage Service is limited to Azure Blob Storage or S3.");
                        throw new ConfigurationErrorsException("Storage Service is limited to Azure Blob Storage or S3.");
                }

                switch (serviceOptions.DatabaseService)
                {
                    case ServiceName.AzureCosmosDB:
                        services.AddCosmosDb(configuration);
                        break;
                    case ServiceName.ApacheCouchDB:
                        services.AddCouchDb(configuration);
                        break;
                    default:
                        Log.Fatal("Database Service is limited to Azure CosmosDB or Apache CouchDB.");
                        throw new ConfigurationErrorsException("Database Service is limited to Azure CosmosDB or Apache CouchDB.");
                }

                services.AddDiscordService(configuration);

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IFc2LiveDLService, Fc2LiveDLService>();

                services.AddSingleton<IUploaderService, UploaderService>();

                services.AddHostedService<MigrationWorker>();
                services.AddHostedService<MonitorWorker>();
                services.AddHostedService<RecordWorker>();
                services.AddSingleton<RecordService>();
                services.AddHostedService<UpdateChannelInfoWorker>();
                services.AddHostedService<UpdateVideoStatusWorker>();
                services.AddHeartbeatWorker(configuration);

                services.AddScoped<VideoService>();
                services.AddScoped<ChannelService>();
                services.AddScoped<RssService>();
                services.AddScoped<YoutubeService>();
                services.AddScoped<TwitcastingService>();
                services.AddTwitchService(configuration);
                services.AddScoped<Fc2Service>();
            });

    IHost host = hostBuilder.Build();

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}
