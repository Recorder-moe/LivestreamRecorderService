using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using LivestreamRecorderService.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;
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

        services.AddOptions<DiscordOption>()
                .Bind(configuration.GetSection(DiscordOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<EcPayOption>()
                .Bind(configuration.GetSection(EcPayOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
        var cosmosDbOptions = services.BuildServiceProvider().GetRequiredService<IOptions<CosmosDbOptions>>().Value;
        var twitchOptions = services.BuildServiceProvider().GetRequiredService<IOptions<TwitchOption>>().Value;
        var discordOptions = services.BuildServiceProvider().GetRequiredService<IOptions<DiscordOption>>().Value;
        var ecPayOptions = services.BuildServiceProvider().GetRequiredService<IOptions<EcPayOption>>().Value;

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
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        services.AddHttpClient("AzureFileShares2BlobContainers", client =>
        {
            client.BaseAddress = new Uri("https://azurefileshares2blobcontainers.azurewebsites.net/");
            // Set this bigger than Azure Function timeout (10min)
            client.Timeout = TimeSpan.FromMinutes(11);
        });

        services.AddAzureClients(clientsBuilder =>
        {
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token));
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddFileServiceClient(azureOptions.ConnectionString);
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddBlobServiceClient(azureOptions.ConnectionString);
        });
        services.AddSingleton<IAFSService, AFSService>();
        services.AddSingleton<IABSService, ABSService>();
        services.AddSingleton<ACIService>();
        services.AddSingleton<ACIYtarchiveService>();
        services.AddSingleton<ACIYtdlpService>();
        services.AddSingleton<ACITwitcastingRecorderService>();
        services.AddSingleton<ACIStreamlinkService>();

        services.AddSingleton<EcPayService>();
        services.AddSingleton<DiscordService>();

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

        services.AddHostedService<RecordWorker>();
        services.AddHostedService<MonitorWorker>();
        services.AddHostedService<UpdateChannelInfoWorker>();
        services.AddHostedService<UpdateVideoStatusWorker>();
        services.AddHostedService<CheckPendingTransactionWorker>();

        services.AddScoped<VideoService>();
        services.AddScoped<ChannelService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<RSSService>();
        services.AddScoped<YoutubeSerivce>();
        services.AddScoped<TwitcastingService>();
        services.AddScoped<TwitchSerivce>();
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

