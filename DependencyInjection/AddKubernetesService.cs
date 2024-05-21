using System.Configuration;
using k8s;
using k8s.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Serilog;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddKubernetesService(this IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            ServiceOption serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

            if (serviceOptions.JobService != ServiceName.Kubernetes) return services;

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

            k8SConfig.Namespace =
                !string.IsNullOrWhiteSpace(kubernetesOptions.Namespace)
                    ? kubernetesOptions.Namespace
                    : "recordermoe";

            var client = new Kubernetes(k8SConfig);
            ensureNamespaceExists(client, k8SConfig.Namespace);
            services.AddSingleton(client);

            services.AddSingleton<IJobService, KubernetesService>();

            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Kubernetes configuration is invalid.");
            throw new ConfigurationErrorsException("Kubernetes configuration is invalid.");
        }

        void ensureNamespaceExists(ICoreV1Operations client, string namespaceName)
        {
            V1Namespace? existingNamespace = client.ListNamespace().Items
                                                   .ToList()
                                                   .Find(ns => ns.Metadata.Name == namespaceName);

            if (existingNamespace != null) return;

            var newNamespace = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = namespaceName
                }
            };

            client.CreateNamespace(newNamespace);
            Log.Information("Namespace {namespaceName} created.", namespaceName);
        }
    }
}
