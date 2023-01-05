using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorderService;
using LivestreamRecorderService.DB.Core;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Serilog;

//#if DEBUG
//Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));
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
                                      .CreateBootstrapLogger();

Log.Information("Starting up...");

//#if DEBUG
//#warning The debug build will print the connection string to the log for debugging purposes.
//Log.Debug(configuration.GetConnectionString("DefaultConnection"));
//#endif

try
{
    IHost host = Host.CreateDefaultBuilder(args)
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

        // Add CosmosDb
        services.AddDbContext<PublicContext>((options) =>
        {
            options.UseCosmos(
                connectionString: configuration.GetConnectionString("Public")!,
                databaseName: configuration.GetSection(CosmosDbOptions.ConfigurationSectionName)
                                           .GetValue<string>(nameof(CosmosDbOptions.DatabaseName))
                              ?? throw new Exception($"Settings misconfigured. Missing {nameof(CosmosDbOptions.DatabaseName)}"));
        });

        services.AddAzureClients(clientsBuilder =>
        {
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token));
        });
        services.AddSingleton<IACIService, ACIService>();
        services.AddSingleton<ACIYtarchiveService>();

        services.AddHostedService<RecordWorker>();

        services.AddScoped<IVideoRepository, VideoRepository>();
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

