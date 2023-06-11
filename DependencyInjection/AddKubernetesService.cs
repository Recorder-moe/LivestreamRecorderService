using k8s;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.Kubernetes;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddKubernetesService(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

                if (serviceOptions.JobService != ServiceName.Kubernetes)
                {
                    return services;
                }

                IConfigurationSection config = configuration.GetSection(KubernetesOption.ConfigurationSectionName);
                var kubernetesOptions = config.Get<KubernetesOption>();
                if (null == kubernetesOptions) throw new ConfigurationErrorsException();

                services.AddOptions<KubernetesOption>()
                        .Bind(config)
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                KubernetesClientConfiguration k8sConfig = 
                    kubernetesOptions.UseTheSameCluster
                    ? KubernetesClientConfiguration.InClusterConfig()
                    : !string.IsNullOrWhiteSpace(kubernetesOptions.ConfigFile)
                        ? KubernetesClientConfiguration.BuildConfigFromConfigFile(kubernetesOptions.ConfigFile)
                        : KubernetesClientConfiguration.BuildDefaultConfig();
                var client = new Kubernetes(k8sConfig);
                services.AddSingleton<Kubernetes>(client);

                services.AddSingleton<IJobService, KubernetesService>();

                KubernetesService.KubernetesNamespace = string.IsNullOrWhiteSpace(kubernetesOptions.Namespace)
                                ? "recorder.moe"
                                : kubernetesOptions.Namespace;

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();

                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Kubernetes configuration is invalid.");
                throw new ConfigurationErrorsException("Kubernetes configuration is invalid.");
            }
        }
    }
}
