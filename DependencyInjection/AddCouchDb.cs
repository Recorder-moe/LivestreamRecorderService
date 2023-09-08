﻿#if COUCHDB
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

                // https://github.com/matteobortolazzo/couchdb-net#dependency-injection
                services.AddCouchContext<CouchDBContext>((options) =>
                {
                    options
                        .UseEndpoint(couchDBOptions.Endpoint)
                        .UseCookieAuthentication(username: couchDBOptions.Username, password: couchDBOptions.Password)
                        .ConfigureFlurlClient(setting =>
                        {
#if !RELEASE
                            setting.BeforeCall = call
                                => Log.Debug("Sending request to couch: {request} {body}", call, call.RequestBody);
                            setting.AfterCallAsync = call => Task.Run(() =>
                            {
                                if (call.Succeeded)
                                {
                                    Log.Debug("Received response from couch: {response} {body}", call, call.Response.ResponseMessage.Content.ReadAsStringAsync().Result);
                                }
                            });
#endif
                            setting.OnErrorAsync = call => Task.Run(()
                                => Log.Error("Request Failed: {request} {body}", call, call?.RequestBody ?? string.Empty));
                        })
                        .SetPropertyCase(PropertyCaseType.None);
                });

                // UnitOfWork is registered as a singleton to align with the registration of CouchContext as a singleton.
                // This also prevents multiple executions of index creation.
                services.AddSingleton<UnitOfWork_Public>();
                services.AddSingleton<UnitOfWork_Private>();

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
