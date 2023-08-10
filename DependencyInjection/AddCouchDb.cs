#if COUCHDB
using CouchDB.Driver.DependencyInjection;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.CouchDB;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Configuration;
#endif
using Serilog;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddCouchDB(this IServiceCollection services, IConfiguration configuration)
        {
#if !COUCHDB
            Log.Fatal("This is a CosmosDB build. Please use the CouchDB build for Apache CouchDB support.");
            throw new InvalidOperationException("This is a CosmosDB build. Please use the CouchDB build for Apache CouchDB support.");
#else
            try
            {
                var couchDBOptions = services.BuildServiceProvider().GetRequiredService<IOptions<CouchDBOption>>().Value;

                if (null == couchDBOptions
                    || string.IsNullOrEmpty(couchDBOptions.Endpoint)
                    || string.IsNullOrEmpty(couchDBOptions.Username)
                    || string.IsNullOrEmpty(couchDBOptions.Password))
                    throw new ConfigurationErrorsException();

                services.AddCouchContext<CouchDBContext>((options) =>
                {
                    options
                        .UseEndpoint(couchDBOptions.Endpoint)
                        .UseCookieAuthentication(username: couchDBOptions.Username, password: couchDBOptions.Password)
#if !COUCHDB_RELEASE && !COSMOSDB_RELEASE
                        .ConfigureFlurlClient(setting
                            => setting.BeforeCall = call
                                => Log.Debug("Sending request to couch: {request} {body}", call, call.RequestBody))
#endif
                        .SetPropertyCase(PropertyCaseType.None);
                });

                services.AddScoped<UnitOfWork_Public>();
                services.AddScoped<UnitOfWork_Private>();
                services.AddScoped<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing CouchDB Settings. Please set CouchDB:Endpoint CouchDB:Username CouchDB:Password in appsettings.json.");
                throw new ConfigurationErrorsException("Missing CouchDB Settings. Please set CouchDB:Endpoint CouchDB:Username CouchDB:Password in appsettings.json.");
            }
#endif
        }
    }
}
