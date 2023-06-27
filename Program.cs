using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.SingletonServices;
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

try
{
    IHost host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        services.AddHttpClient();

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

        switch (serviceOptions.JobService)
        {
            case ServiceName.AzureContainerInstance:
                services.AddAzureContainerInstanceService();
                break;
            case ServiceName.Kubernetes:
                services.AddKubernetesService(configuration);
                break;
            case ServiceName.Docker:
                Log.Fatal("Currently only Azure Container Instance and K8s are supported.");
                throw new NotImplementedException("Currently only Azure Container Instance and K8s are supported.");
            default:
                Log.Fatal("Job Serivce is limited to Azure Container Instance, Kubernetes or Docker.");
                throw new ConfigurationErrorsException("Job Serivce is limited to Azure Container Instance, Kubernetes or Docker.");
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
                if (serviceOptions.JobService == ServiceName.Kubernetes)
                {
                    Log.Warning("If you are using Azure File Share with Kubernetes other than AKS, ensure that you have set up the Azure File CSI Driver.");
                }
                break;
            case ServiceName.DockerVolume:
                Log.Fatal("Currently only AzureFileShare and NFS is supported.");
                throw new NotImplementedException("Currently only AzureFileShare and NFS is supported.");

            //if (serviceOptions.JobService == ServiceName.AzureContainerInstance)
            //{
            //    Log.Fatal("Azure Container Instance is not able to mount Docker volume. Use Azure File Share instead.");
            //    throw new ConfigurationErrorsException("Azure Container Instance is not able to mount Docker volume. Use Azure File Share instead.");
            //}
            case ServiceName.NFS:
                if (string.IsNullOrWhiteSpace(nfsOption.Server)
                    || string.IsNullOrWhiteSpace(nfsOption.Path))
                {
                    Log.Fatal("NFS server and path must be specified.");
                    throw new ConfigurationErrorsException("NFS server and path must be specified.");
                }
                if (serviceOptions.JobService == ServiceName.AzureContainerInstance)
                {
                    Log.Fatal("Azure Container Instance is not able to mount NFS volume. Use Azure File Share instead.");
                    throw new ConfigurationErrorsException("Azure Container Instance is not able to mount NFS volume. Use Azure File Share instead.");
                }
                break;
            default:
                Log.Fatal("Shared Volume Serivce is limited to Azure File Share, DockerVolume or NFS.");
                throw new ConfigurationErrorsException("Shared Volume Serivce is limited to Azure File Share, DockerVolume or NFS.");
        }

        switch (serviceOptions.StorageService)
        {
            case ServiceName.AzureBlobStorage:
                services.AddAzuerBlobStorageService();
                break;
            case ServiceName.S3:
                Log.Fatal("Currently only Azure Blob Storage is supported.");
                throw new NotImplementedException("Currently only Azure Blob Storage is supported.");
            default:
                Log.Fatal("Storage Serivce is limited to Azure Blob Storage, NFS or S3.");
                throw new ConfigurationErrorsException("Storage Serivce is limited to Azure Blob Storage, NFS or S3.");
        }

        switch (serviceOptions.DatabaseService)
        {
            case ServiceName.AzureCosmosDB:
                services.AddCosmosDB(configuration);
                break;
            case ServiceName.ApacheCouchDB:
                Log.Fatal("Currently only Azure CosmosDB is supported.");
                throw new NotImplementedException("Currently only Azure CosmosDB is supported.");
            default:
                Log.Fatal("Database Serivce is limited to Azure CosmosDB or Apache CouchDB.");
                throw new ConfigurationErrorsException("Database Serivce is limited to Azure CosmosDB or Apache CouchDB.");
        }

        services.AddDiscordService(configuration);

        services.AddHostedService<RecordWorker>();
        services.AddSingleton<RecordService>();
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
