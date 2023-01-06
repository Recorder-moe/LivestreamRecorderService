using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices
{
    public class ACIYtarchiveService : ACIService, IACIService
    {
        private readonly AzureOption _azureOption;

        public ACIYtarchiveService(
            ArmClient armClient,
            IOptions<AzureOption> options) : base(armClient, options)
        {
            _azureOption = options.Value;
        }

        public Task<ArmOperation<ArmDeploymentResource>> StartInstanceAsync(string videoId, CancellationToken cancellation = default)
            => CreateAzureContainerInstanceAsync(
                template: "ACI_ytarchive.json",
                parameters: new
                {
                    containerName = new
                    {
                        value = videoId.ToLower().Replace("_", "")  // [a-z0-9]([-a-z0-9]*[a-z0-9])?
                    },
                    commandOverrideArray = new
                    {
                        value = new string[] {
                            "/usr/bin/dumb-init", "--",
                            "/ytarchive", "--add-metadata",
                                          "--merge",
                                          "--retry-frags", "30",
                                          "--thumbnail",
                                          "--write-thumbnail",
                                          "--write-description",
                                          "-o", "%(id)s",
                                          "https://www.youtube.com/watch?v=" + videoId,
                                          "best"
                        }
                    },
                    storageAccountName = new
                    {
                        value = _azureOption.StorageAccountName
                    },
                    storageAccountKey = new
                    {
                        value = _azureOption.StorageAccountKey
                    },
                    fileshareVolumeName = new
                    {
                        value = "livestream-recorder"
                    }
                },
                deploymentName: videoId,
                cancellation: cancellation);

    }
}
