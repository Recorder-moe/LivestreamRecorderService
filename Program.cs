using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.DependencyInjection;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using LivestreamRecorderService.SingletonServices;
using LivestreamRecorderService.SingletonServices.ACI;
using LivestreamRecorderService.Workers;
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
        var twitchOptions = services.BuildServiceProvider().GetRequiredService<IOptions<TwitchOption>>().Value;
        var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

        services.AddDatabase(configuration);

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

        switch (serviceOptions.ContainerService)
        {
            case ServiceName.AzureContainerInstance:
                if (null == azureOptions.AzureContainerInstance
                    || string.IsNullOrEmpty(azureOptions.AzureContainerInstance.ClientSecret.ClientID)
                    || string.IsNullOrEmpty(azureOptions.AzureContainerInstance.ClientSecret.ClientSecret))
                {
                    Log.Fatal("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
                }
                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.UseCredential((options)
                        => new ClientSecretCredential(tenantId: azureOptions.AzureContainerInstance.ClientSecret.TenantID,
                                                      clientId: azureOptions.AzureContainerInstance.ClientSecret.ClientID,
                                                      clientSecret: azureOptions.AzureContainerInstance.ClientSecret.ClientSecret))
                                     .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token)));

                services.AddSingleton<IJobService, ACIService>();

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();
                break;
            case ServiceName.K8s:
                // TODO K8s
                throw new NotImplementedException("K8s is not implemented yet.");
            default:
                Log.Fatal("Currently only Azure Container Instance and K8s are supported.");
                throw new NotImplementedException("Currently only Azure Container Instance and K8s are supported.");
        }

        switch (serviceOptions.PresistentVolumeService)
        {
            case ServiceName.AzureFileShare:
                if (null == azureOptions.AzureFileShare)
                {
                    Log.Fatal("Missing AzureFileShare. Please set Azure:AzureFileShare in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing AzureFileShare. Please set Azure:AzureFileShare in appsettings.json.");
                }

                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.AddFileServiceClient(azureOptions.AzureFileShare.ConnectionString));
                services.AddSingleton<IAFSService, AFSService>();
                break;
            default:
                Log.Fatal("Currently only Azure File Share is supported.");
                throw new NotImplementedException("Currently only Azure File Share is supported.");
        }

        switch (serviceOptions.StorageService)
        {
            case ServiceName.AzureBlobStorage:
                if (null == azureOptions.AzuerBlobStorage)
                {
                    Log.Fatal("Missing AzuerBlobStorage. Please set Azure:AzuerBlobStorage in appsettings.json.");
                    throw new ConfigurationErrorsException("Missing AzuerBlobStorage. Please set Azure:AzuerBlobStorage in appsettings.json.");
                }
                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.AddBlobServiceClient(azureOptions.AzuerBlobStorage.ConnectionString));

                services.AddSingleton<IABSService, ABSService>();
                break;
            default:
                Log.Fatal("Currently only Azure Blob Storage is supported.");
                throw new NotImplementedException("Currently only Azure Blob Storage is supported.");
        }

        services.AddDiscordService(configuration);

        if (twitchOptions.Enabled)
        {
            if (string.IsNullOrEmpty(twitchOptions.ClientId)
             || string.IsNullOrEmpty(twitchOptions.ClientSecret))
            {
                Log.Fatal("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
                throw new ConfigurationErrorsException("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
            }
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
