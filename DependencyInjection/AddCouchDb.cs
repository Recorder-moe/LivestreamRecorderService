#if COUCHDB
using CouchDB.Driver.DependencyInjection;
using LivestreamRecorder.DB.CouchDB;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
#endif
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddCouchDB(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
#if COUCHDB
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
                        .UseCookieAuthentication(username: couchDBOptions.Username, password: couchDBOptions.Password);
                });

                services.AddScoped<UnitOfWork_Public>();
                services.AddScoped<UnitOfWork_Private>();
                services.AddScoped<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif
                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing CouchDB Settings. Please set CouchDB:Endpoint CouchDB:Username CouchDB:Password in appsettings.json.");
                throw new ConfigurationErrorsException("Missing CouchDB Settings. Please set CouchDB:Endpoint CouchDB:Username CouchDB:Password in appsettings.json.");
            }
        }
    }
}
