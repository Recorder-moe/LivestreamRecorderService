#if COUCHDB
using CouchDB.Driver.DependencyInjection;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.CouchDB;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Configuration;
using System.Net.Sockets;
using Flurl.Http;
using Flurl.Http.Configuration;
using Polly;
#endif
using Polly.Retry;
using Serilog;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddCouchDb(this IServiceCollection services, IConfiguration configuration)
    {
#if !COUCHDB
        Log.Fatal("This is a CosmosDB build. Please use the CouchDB build for Apache CouchDB support.");
        throw new InvalidOperationException("This is a CosmosDB build. Please use the CouchDB build for Apache CouchDB support.");
#else
        try
        {
            CouchDbOption? couchDbOptions = services.BuildServiceProvider().GetRequiredService<IOptions<CouchDbOption>>().Value;

            if (null == couchDbOptions
                || string.IsNullOrEmpty(couchDbOptions.Endpoint)
                || string.IsNullOrEmpty(couchDbOptions.Username)
                || string.IsNullOrEmpty(couchDbOptions.Password))
                throw new ConfigurationErrorsException();

            services.AddSingleton<CouchDbHttpClientFactory>();

            // https://github.com/matteobortolazzo/couchdb-net#dependency-injection
            services.AddCouchContext<CouchDBContext>((options) =>
            {
                options
                    .UseEndpoint(couchDbOptions.Endpoint)
                    .UseCookieAuthentication(username: couchDbOptions.Username, password: couchDbOptions.Password)
                    .ConfigureFlurlClient(setting =>
                    {
                        // Always use the same HttpClient instance to optimize resource usage and performance.
                        setting.HttpClientFactory =
                            services.BuildServiceProvider().GetRequiredService<CouchDbHttpClientFactory>(); // Configure HTTP request timeout

                        setting.Timeout = TimeSpan.FromSeconds(30);

                        // Configure network error handling and retry mechanism
                        setting.OnError = call =>
                        {
                            if (call.Exception != null)
                                Log.Error(exception: call.Exception,
                                          messageTemplate: "CouchDB request failed: {Url}",
                                          propertyValue: call.Request?.Url);
                        };

                        setting.OnErrorAsync = async call =>
                        {
                            // Don't retry requests to /_session endpoint to avoid authentication lockout
                            if (call.Request?.Url?.ToString()?.Contains("/_session") == true)
                            {
                                return;
                            }

                            // Check if this is a retryable error
                            bool shouldRetry = call.Exception switch
                            {
                                HttpRequestException => true,
                                TaskCanceledException => true,
                                SocketException => true,
                                IOException => true,
                                FlurlHttpException httpEx => httpEx.StatusCode >= 500 ||
                                                             httpEx.StatusCode == 408 ||
                                                             httpEx.StatusCode == 429,
                                _ => false
                            };

                            if (shouldRetry && call.Request != null)
                            {
                                // Create Polly retry strategy
                                ResiliencePipeline retryPipeline = new ResiliencePipelineBuilder()
                                                                   .AddRetry(new RetryStrategyOptions
                                                                   {
                                                                       ShouldHandle = new PredicateBuilder()
                                                                                      .Handle<HttpRequestException>()
                                                                                      .Handle<TaskCanceledException>()
                                                                                      .Handle<SocketException>()
                                                                                      .Handle<IOException>()
                                                                                      .Handle<FlurlHttpException>(ex =>
                                                                                          ex.StatusCode >= 500 ||
                                                                                          ex.StatusCode == 408 ||
                                                                                          ex.StatusCode == 429),
                                                                       Delay = TimeSpan.FromSeconds(2),
                                                                       MaxRetryAttempts = 3,
                                                                       BackoffType = DelayBackoffType.Exponential,
                                                                       UseJitter = true,
                                                                       OnRetry = args =>
                                                                       {
                                                                           Log.Warning(
                                                                               messageTemplate:
                                                                               "CouchDB connection retry {AttemptNumber}/{MaxAttempts}, error: {Exception}",
                                                                               propertyValue0: args.AttemptNumber + 1,
                                                                               propertyValue1: 3,
                                                                               propertyValue2: args.Outcome.Exception?.Message);

                                                                           return ValueTask.CompletedTask;
                                                                       }
                                                                   })
                                                                   .Build();

                                try
                                {
                                    // Use Polly strategy to retry the request
                                    IFlurlResponse? result = await retryPipeline.ExecuteAsync(async (token) =>
                                    {
                                        Log.Information(messageTemplate: "Retrying CouchDB request: {Url}", propertyValue: call.Request.Url);

                                        // Recreate the same request
                                        IFlurlRequest? retryRequest = new Flurl.Url(call.Request.Url).WithClient(call.Request.Client);

                                        // Copy headers
                                        foreach ((string Name, string Value) header in call.Request.Headers)
                                            retryRequest = retryRequest.WithHeader(name: header.Name, value: header.Value);

                                        return call.HttpRequestMessage?.Method?.Method switch
                                        {
                                            "POST" when call.RequestBody != null =>
                                                await retryRequest.PostJsonAsync(data: call.RequestBody, cancellationToken: token),
                                            "PUT" when call.RequestBody != null =>
                                                await retryRequest.PutJsonAsync(data: call.RequestBody, cancellationToken: token),
                                            "PATCH" when call.RequestBody != null =>
                                                await retryRequest.PatchJsonAsync(data: call.RequestBody, cancellationToken: token),
                                            "DELETE" =>
                                                await retryRequest.DeleteAsync(cancellationToken: token),
                                            "HEAD" =>
                                                await retryRequest.HeadAsync(cancellationToken: token),
                                            "OPTIONS" =>
                                                await retryRequest.OptionsAsync(cancellationToken: token),
                                            _ =>
                                                await retryRequest.GetAsync(cancellationToken: token)
                                        };
                                    }); // Update call response

                                    call.Response = result;
                                    call.ExceptionHandled = true;

                                    Log.Information(messageTemplate: "CouchDB request retry successful: {Url}", propertyValue: call.Request.Url);
                                }
                                catch (Exception retryException)
                                {
                                    Log.Error(exception: retryException,
                                              messageTemplate: "CouchDB request retry ultimately failed: {Url}, will throw original error",
                                              propertyValue: call.Request.Url);
                                    // Don't handle retry failure, let original error be thrown
                                }
                            }
                        };

#if !RELEASE
                        setting.BeforeCall = call
                            => Log.Debug(messageTemplate: "Sending request to CouchDB: {request} {body}",
                                         propertyValue0: call,
                                         propertyValue1: call.RequestBody);

                        setting.AfterCallAsync = call => Task.Run(() =>
                        {
                            if (call.Succeeded)
                                Log.Debug(messageTemplate: "Received CouchDB response: {response} {body}",
                                          propertyValue0: call,
                                          propertyValue1: call.Response.ResponseMessage.Content.ReadAsStringAsync().Result);
                        });
#endif
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
            throw new ConfigurationErrorsException(
                "Missing CouchDB Settings. Please set CouchDB:Endpoint CouchDB:Username CouchDB:Password in appsettings.json.");
        }
#endif
    }

#if COUCHDB
    /// <summary>
    /// Custom HTTP client factory for CouchDB interactions.
    /// </summary>
    /// <remarks>
    /// This factory provides a globally shared instance of HttpClient to optimize resource usage and performance.
    /// </remarks>
    private class CouchDbHttpClientFactory : DefaultHttpClientFactory
    {
        private static HttpClient? _httpClient;

        public override HttpClient CreateHttpClient(HttpMessageHandler handler)
            => _httpClient ??= base.CreateHttpClient(handler);
    }
#endif
}
