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

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

#if !RELEASE
Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
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
    var hostBuilder =
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

                var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;
                var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;

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
                        Log.Fatal("Job Service is limited to Azure Container Instance, Kubernetes or Docker.");
                        throw new ConfigurationErrorsException(
                            "Job Service is limited to Azure Container Instance, Kubernetes or Docker.");
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
                            throw new ConfigurationErrorsException(
                                "AzureFileShare StorageAccountName, StorageAccountKey, ShareName must be specified.");
                        }

                        if (serviceOptions.JobService == ServiceName.Kubernetes)
                        {
                            Log.Warning(
                                "If you are using Azure File Share with Kubernetes other than AKS, ensure that you have set up the Azure File CSI Driver.");
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
                    case ServiceName.CustomPVC:
                        if (serviceOptions.JobService != ServiceName.Kubernetes)
                        {
                            Log.Fatal("CustomPVC is only supported in Kubernetes.");
                            throw new ConfigurationErrorsException("CustomPVC is only supported in Kubernetes.");
                        }

                        var k8SOption = services.BuildServiceProvider().GetRequiredService<IOptions<KubernetesOption>>()
                                                .Value;

                        if (string.IsNullOrEmpty(k8SOption.PVCName))
                        {
                            Log.Fatal(
                                "When selected the CustomPVC for SharedVolumeService, it is necessary to specify the Kubernetes.PVCName.");

                            throw new ConfigurationErrorsException(
                                "When selected the CustomPVC for SharedVolumeService, it is necessary to specify the Kubernetes.PVCName.");
                        }

                        Log.Warning(
                            $"CustomPVC has been selected as the SharedVolumeService. Please ensure that you have already prepared the PersistentVolumeClaim with the name {k8SOption.PVCName} in the Namespace {k8SOption.Namespace}.");

                        break;
                    default:
                        Log.Fatal("Shared Volume Service is limited to Azure File Share, DockerVolume, NFS or CustomPVC(k8s).");
                        throw new ConfigurationErrorsException(
                            "Shared Volume Service is limited to Azure File Share, DockerVolume or NFS.");
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

    var host = hostBuilder.Build();

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
