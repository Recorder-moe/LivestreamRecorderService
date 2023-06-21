using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.Workers;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

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

        services.AddOptions<NFSOption>()
                .Bind(configuration.GetSection(NFSOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;
        var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
        var nfsOption = services.BuildServiceProvider().GetRequiredService<IOptions<NFSOption>>().Value;

        switch (serviceOptions.DatabaseService)
        {
            case ServiceName.AzureCosmosDb:
                services.AddCosmosDb(configuration);
                break;
            default:
                Log.Fatal("Currently only Azure CosmosDb is supported.");
                throw new NotImplementedException("Currently only Azure CosmosDb is supported.");
        }

        switch (serviceOptions.JobService)
        {
            case ServiceName.AzureContainerInstance:
                services.AddAzureContainerInstanceService();
                break;
            case ServiceName.Kubernetes:
                services.AddKubernetesService(configuration);
                break;
            default:
                Log.Fatal("Currently only Azure Container Instance and K8s are supported.");
                throw new NotImplementedException("Currently only Azure Container Instance and K8s are supported.");
        }

        switch (serviceOptions.SharedVolumeService)
        {
            case ServiceName.AzureFileShare:
                if (null == azureOptions.FileShare
                    || string.IsNullOrEmpty(azureOptions.FileShare.StorageAccountName)
                    || string.IsNullOrEmpty(azureOptions.FileShare.StorageAccountKey)
                    || string.IsNullOrEmpty(azureOptions.FileShare.ShareName))
                {
                    Log.Fatal("AzureFileShare StorageAccountName, StorageAccountKey, ShareName must be specified.");
                    throw new ConfigurationErrorsException("AzureFileShare StorageAccountName, StorageAccountKey, ShareName must be specified.");
                }
                break;
            case ServiceName.DockerVolume:
                Log.Fatal("Currently only AzureFileShare is supported.");
                throw new NotImplementedException("Currently only AzureFileShare is supported.");
            case ServiceName.NFS:
                goto case ServiceName.DockerVolume;

                //if (string.IsNullOrWhiteSpace(nfsOption.Server)
                //    || string.IsNullOrWhiteSpace(nfsOption.Path))
                //{
                //    Log.Fatal("NFS server and path must be specified.");
                //    throw new ConfigurationErrorsException("NFS server and path must be specified.");
                //}
                //break;
            default:
                Log.Fatal("Shared Volume Serivce is limited to Azure File Share, DockerVolume or NFS.");
                throw new ConfigurationErrorsException("Shared Volume Serivce is limited to Azure File Share, DockerVolume or NFS.");
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

        {
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
