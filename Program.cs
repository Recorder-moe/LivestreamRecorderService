using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.SingletonServices;
using LivestreamRecorderService.SingletonServices.ACI;
using LivestreamRecorderService.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Interfaces;

//#if DEBUG
Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));
//#endif


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

        services.AddOptions<CosmosDbOptions>()
                .Bind(configuration.GetSection(CosmosDbOptions.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<TwitchOption>()
                .Bind(configuration.GetSection(TwitchOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<HeartbeatOption>()
                .Bind(configuration.GetSection(HeartbeatOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<ServiceOption>()
                .Bind(configuration.GetSection(ServiceOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
        var cosmosDbOptions = services.BuildServiceProvider().GetRequiredService<IOptions<CosmosDbOptions>>().Value;
        var twitchOptions = services.BuildServiceProvider().GetRequiredService<IOptions<TwitchOption>>().Value;
        var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

        if (serviceOptions.DatabaseService == ServiceName.AzureCosmosDB)
        {
            // Add CosmosDb
            services.AddDbContext<PublicContext>((options) =>
            {
                options
                    //.EnableSensitiveDataLogging()
                    .UseCosmos(connectionString: configuration.GetConnectionString("Public")!,
                               databaseName: cosmosDbOptions.Public.DatabaseName,
                               cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });
            services.AddDbContext<PrivateContext>((options) =>
            {
                options
                    //.EnableSensitiveDataLogging()
                    .UseCosmos(connectionString: configuration.GetConnectionString("Private")!,
                               databaseName: cosmosDbOptions.Private.DatabaseName,
                               cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });

            services.AddScoped<UnitOfWork_Public>();
            services.AddScoped<UnitOfWork_Private>();
            services.AddScoped<IVideoRepository, VideoRepository>();
            services.AddScoped<IChannelRepository, ChannelRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
        }
        else
        {
            Log.Fatal("Currently only Azure CosmosDB is supported.");
            throw new NotImplementedException("Currently only Azure CosmosDB is supported.");
        }

        if (serviceOptions.PresistentVolumeService == ServiceName.AzureFileShare
            && serviceOptions.StorageService == ServiceName.AzureBlobStorage)
        {
            services.AddHttpClient("AzureFileShares2BlobContainers", client =>
            {
                client.BaseAddress = new Uri("https://azurefileshares2blobcontainers.azurewebsites.net/");
                // Set this bigger than Azure Function timeout (10min)
                client.Timeout = TimeSpan.FromMinutes(11);
            });
        }

        services.AddAzureClients(clientsBuilder =>
        {
            if (serviceOptions.ContainerService == ServiceName.AzureContainerInstance
                || serviceOptions.PresistentVolumeService == ServiceName.AzureFileShare
                || serviceOptions.StorageService == ServiceName.AzureBlobStorage)
            {
                var values = Environment.GetEnvironmentVariables();
                if (!values.Contains("AZURE_CLIENT_SECRET") || string.IsNullOrEmpty(string.Format("{0}", values["AZURE_CLIENT_SECRET"]))
                 || !values.Contains("AZURE_CLIENT_ID") || string.IsNullOrEmpty(string.Format("{0}", values["AZURE_CLIENT_ID"]))
                 || !values.Contains("AZURE_TENANT_ID") || string.IsNullOrEmpty(string.Format("{0}", values["AZURE_TENANT_ID"]))
                )
                {
                    Log.Fatal("Missing Azure Credentials. Please set environment variables AZURE_CLIENT_SECRET, AZURE_CLIENT_ID and AZURE_TENANT_ID.");
                    throw new ConfigurationErrorsException("Missing Azure Credentials. Please set environment variables AZURE_CLIENT_SECRET, AZURE_CLIENT_ID and AZURE_TENANT_ID.");
                }
            }
            if (serviceOptions.ContainerService == ServiceName.AzureContainerInstance)
            {
                clientsBuilder.UseCredential(new DefaultAzureCredential())
                              .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token));
            }
            if (serviceOptions.PresistentVolumeService == ServiceName.AzureFileShare)
            {
                clientsBuilder.UseCredential(new DefaultAzureCredential())
                              .AddFileServiceClient(azureOptions.ConnectionString);
            }
            if (serviceOptions.StorageService == ServiceName.AzureBlobStorage)
            {
                clientsBuilder.UseCredential(new DefaultAzureCredential())
                              .AddBlobServiceClient(azureOptions.ConnectionString);
            }
        });

        switch (serviceOptions.PresistentVolumeService)
        {
            case ServiceName.AzureFileShare:
                if (string.IsNullOrEmpty(azureOptions.ShareName))
                {
                    Log.Fatal("Missing Azure File Share Name. Please set Azure:ShareName in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing Azure File Share Name. Please set Azure:ShareName in appsettings.json.");
                }
                services.AddSingleton<IAFSService, AFSService>();
                break;
            default:
                Log.Fatal("Currently only Azure File Share is supported.");
                throw new NotImplementedException("Currently only Azure File Share is supported.");
        }

        switch (serviceOptions.StorageService)
        {
            case ServiceName.AzureBlobStorage:
                if (string.IsNullOrEmpty(azureOptions.BlobContainerName)
                         || string.IsNullOrEmpty(azureOptions.BlobContainerNamePublic))
                {
                    Log.Fatal("Missing Azure Blob Container Name. Please set Azure:BlobContainerName in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing Azure Blob Container Name. Please set Azure:BlobContainerName in appsettings.json.");
                }
                services.AddSingleton<IABSService, ABSService>();
                break;
            default:
                Log.Fatal("Currently only Azure Blob Storage is supported.");
                throw new NotImplementedException("Currently only Azure Blob Storage is supported.");
        }

        switch (serviceOptions.ContainerService)
        {
            case ServiceName.AzureContainerInstance:
                if (string.IsNullOrEmpty(azureOptions.ResourceGroupName)
                         || string.IsNullOrEmpty(azureOptions.StorageAccountName)
                         || string.IsNullOrEmpty(azureOptions.StorageAccountKey)
                        )
                {
                    Log.Fatal("Missing Azure Resource Group Name. Please set Azure:ResourceGroupName in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing Azure Resource Group Name. Please set Azure:ResourceGroupName in appsettings.json.");
                }
                services.AddSingleton<IJobService, ACIService>();

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();
                break;
            case ServiceName.K8s:
                // TODO K8s
                services.AddSingleton<IJobService, ACIService>();

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();
                break;
            default:
                Log.Fatal("Currently only Azure Container Instance and K8s are supported.");
                throw new NotImplementedException("Currently only Azure Container Instance and K8s are supported.");
        }

        services.AddDiscordService(configuration);

        if (twitchOptions.Enabled)
        {
            if (!string.IsNullOrEmpty(twitchOptions.ClientId)
             && !string.IsNullOrEmpty(twitchOptions.ClientSecret))
            {
                services.AddSingleton<ITwitchAPI, TwitchAPI>(s =>
                {
                    var api = new TwitchAPI(
                        loggerFactory: s.GetRequiredService<ILoggerFactory>(),
                        settings: new ApiSettings()
                        {
                            ClientId = twitchOptions.ClientId,
                            Secret = twitchOptions.ClientSecret
                        });
                    return api;
                });
            }
            else
            {
                Log.Fatal("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
                throw new ConfigurationErrorsException("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
            }
        }

        services.AddHostedService<RecordWorker>();
        services.AddHostedService<MonitorWorker>();
        services.AddHostedService<UpdateChannelInfoWorker>();
        services.AddHostedService<UpdateVideoStatusWorker>();
        services.AddHostedService<HeartbeatWorker>();

        services.AddScoped<VideoService>();
        services.AddScoped<ChannelService>();
        services.AddScoped<RSSService>();
        services.AddScoped<YoutubeService>();
        services.AddScoped<TwitcastingService>();
        if (twitchOptions.Enabled)
        {
            services.AddScoped<TwitchService>();
        }
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
