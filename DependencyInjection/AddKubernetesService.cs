using k8s;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using LivestreamRecorderService.SingletonServices.Kubernetes;
using LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddKubernetesService(this IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            ServiceOption serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

            if (serviceOptions.JobService != ServiceName.Kubernetes)
            {
                return services;
            }

            IConfigurationSection config = configuration.GetSection(KubernetesOption.ConfigurationSectionName);
            KubernetesOption? kubernetesOptions = config.Get<KubernetesOption>();
            if (null == kubernetesOptions) throw new ConfigurationErrorsException();

            services.AddOptions<KubernetesOption>()
                    .Bind(config)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            KubernetesClientConfiguration k8SConfig =
                // skipcq: CS-R1114
                kubernetesOptions.UseTheSameCluster
                    ? KubernetesClientConfiguration.InClusterConfig()
                    : !string.IsNullOrWhiteSpace(kubernetesOptions.ConfigFile)
                        ? KubernetesClientConfiguration.BuildConfigFromConfigFile(kubernetesOptions.ConfigFile)
                        : KubernetesClientConfiguration.BuildDefaultConfig();

            var client = new Kubernetes(k8SConfig);
            services.AddSingleton(client);

            services.AddSingleton<IJobService, KubernetesService>();

            KubernetesService.KubernetesNamespace = string.IsNullOrWhiteSpace(kubernetesOptions.Namespace)
                ? "recordermoe"
                : kubernetesOptions.Namespace;

            services.AddSingleton<IYtarchiveService, YtarchiveService>();
            services.AddSingleton<IYtdlpService, YtdlpService>();
            services.AddSingleton<IStreamlinkService, StreamlinkService>();
            services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
            services.AddSingleton<IFc2LiveDLService, Fc2LiveDLService>();

            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Kubernetes configuration is invalid.");
            throw new ConfigurationErrorsException("Kubernetes configuration is invalid.");
        }
    }
}
