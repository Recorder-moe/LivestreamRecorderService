using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorderService;
using LivestreamRecorderService.DB.Core;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;

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

        var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
        var cosmosDbOptions = services.BuildServiceProvider().GetRequiredService<IOptions<CosmosDbOptions>>().Value;

        // Add CosmosDb
        services.AddDbContext<PublicContext>((options) =>
        {
            options
                //.EnableSensitiveDataLogging()
                .UseCosmos(connectionString: configuration.GetConnectionString("Public")!,
                           databaseName: cosmosDbOptions.DatabaseName);
        });

        services.AddHttpClient("AzureFileShares2BlobContainers", client =>
        {
            client.BaseAddress = new Uri("https://azurefileshares2blobcontainers.azurewebsites.net/api/");
            // Set this bigger than Azure Function timeout (10min)
            client.Timeout = TimeSpan.FromMinutes(11);
        });

        services.AddAzureClients(clientsBuilder =>
        {
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token));
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddFileServiceClient(azureOptions.ConnectionString);
        });
        services.AddSingleton<IAFSService, AFSService>();
        //services.AddSingleton<IACIService, ACIService>();
        services.AddSingleton<ACIYtarchiveService>();
        services.AddSingleton<ACIYtdlpService>();
        services.AddSingleton<ACITwitcastingRecorderService>();

        services.AddHostedService<RecordWorker>();
        services.AddHostedService<MonitorWorker>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IFileRepository, FileRepository>();

        services.AddScoped<VideoService>();
        services.AddScoped<RSSService>();
        services.AddScoped<YoutubeSerivce>();
        services.AddScoped<TwitcastingService>();
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

