using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorderService;
using LivestreamRecorderService.Models.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
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
    .ConfigureServices(services =>
    {
        services.AddOptions<AzureOption>()
                .Bind(configuration.GetSection(AzureOption.ConfigurationSectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddAzureClients(clientsBuilder =>
        {
            clientsBuilder.UseCredential(new DefaultAzureCredential())
                          .AddClient<ArmClient, ArmClientOptions>((options, token)=> new ArmClient(token));
        });
        services.AddSingleton<IACIService, ACIService>();
        services.AddSingleton<ACIYtarchiveService>();

        services.AddHostedService<RecordWorker>();
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

