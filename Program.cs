using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.Workers;
using Microsoft.Extensions.Options;
using Serilog;

#if DEBUG
Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
#endif


IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#if DEBUG
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

//#if DEBUG
//#warning The debug build will print the connection string to the log for debugging purposes.
//Log.Debug(configuration.GetConnectionString("DefaultConnection"));
//#endif

try
{
    IHost host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        services.AddOptions<AzureOption>()
                .Bind(configuration.GetSection(AzureOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<ServiceOption>()
                .Bind(configuration.GetSection(ServiceOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;
        var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;

        switch (serviceOptions.DatabaseService)
        {
            case ServiceName.AzureCosmosDb:
                services.AddCosmosDb(configuration);
                break;
            default:
                Log.Fatal("Currently only Azure CosmosDb is supported.");
                throw new NotImplementedException("Currently only Azure CosmosDb is supported.");
        }

        if (serviceOptions.PersistentVolumeService == ServiceName.AzureFileShare
            && serviceOptions.StorageService == ServiceName.AzureBlobStorage
            && !string.IsNullOrEmpty(azureOptions.AzureFileShares2BlobContainers))
        {
            services.AddHttpClient("AzureFileShares2BlobContainers", client =>
            {
                client.BaseAddress = new Uri(azureOptions.AzureFileShares2BlobContainers);
                // Set this bigger than Azure Function timeout (10min)
                client.Timeout = TimeSpan.FromMinutes(11);
            });
        }

        switch (serviceOptions.JobService)
        {
            case ServiceName.AzureContainerInstance:
                services.AddAzureContainerInstanceService();
                break;
            case ServiceName.K8s:
                throw new NotImplementedException("K8s is not implemented yet.");
            default:
                Log.Fatal("Currently only Azure Container Instance and K8s are supported.");
                throw new NotImplementedException("Currently only Azure Container Instance and K8s are supported.");
        }

        switch (serviceOptions.PersistentVolumeService)
        {
            case ServiceName.AzureFileShare:
                services.AddAzureFileShareService();
                break;
            default:
                Log.Fatal("Currently only Azure File Share is supported.");
                throw new NotImplementedException("Currently only Azure File Share is supported.");
        }

        switch (serviceOptions.StorageService)
        {
            case ServiceName.AzureBlobStorage:
                services.AddAzuerBlobStorageService();
                break;
            default:
                Log.Fatal("Currently only Azure Blob Storage is supported.");
                throw new NotImplementedException("Currently only Azure Blob Storage is supported.");
        }

        services.AddDiscordService(configuration);

        services.AddHostedService<RecordWorker>();
        services.AddHostedService<MonitorWorker>();
        services.AddHostedService<UpdateChannelInfoWorker>();
        services.AddHostedService<UpdateVideoStatusWorker>();
        services.AddHeartbeatWorker(configuration);

        services.AddScoped<VideoService>();
        services.AddScoped<ChannelService>();
        services.AddScoped<RSSService>();
        services.AddScoped<YoutubeService>();
        services.AddScoped<TwitcastingService>();
        services.AddTwitchService(configuration);
        services.AddScoped<FC2Service>();
    })
    .Build();

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
