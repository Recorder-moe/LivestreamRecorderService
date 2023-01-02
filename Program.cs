using LivestreamRecorderService;
using Serilog;

#if DEBUG
Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));
#endif


IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#if DEBUG
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
#endif
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
        services.AddHostedService<Worker>();
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

