using k8s;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.Kubernetes;
using LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;
using LivestreamRecorderService.SingletonServices.Kubernetes.Uploader;
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
                // skipcq: CS-R1114
                kubernetesOptions.UseTheSameCluster
                ? KubernetesClientConfiguration.InClusterConfig()
                : !string.IsNullOrWhiteSpace(kubernetesOptions.ConfigFile)
                    ? KubernetesClientConfiguration.BuildConfigFromConfigFile(kubernetesOptions.ConfigFile)
                    : KubernetesClientConfiguration.BuildDefaultConfig();
            var client = new Kubernetes(k8sConfig);
            services.AddSingleton<Kubernetes>(client);

            services.AddSingleton<IJobService, KubernetesService>();

            KubernetesService.KubernetesNamespace = string.IsNullOrWhiteSpace(kubernetesOptions.Namespace)
                            ? "recordermoe"
                            : kubernetesOptions.Namespace;

            services.AddSingleton<IYtarchiveService, YtarchiveService>();
            services.AddSingleton<IYtdlpService, YtdlpService>();
            services.AddSingleton<IStreamlinkService, StreamlinkService>();
            services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
            services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();

            services.AddSingleton<IAzureUploaderService, AzureUploaderService>();
            services.AddSingleton<IS3UploaderService, S3UploaderService>();

            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Kubernetes configuration is invalid.");
            throw new ConfigurationErrorsException("Kubernetes configuration is invalid.");
        }
    }
}
