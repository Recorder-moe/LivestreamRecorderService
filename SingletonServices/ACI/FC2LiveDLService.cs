﻿using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class FC2LiveDLService : ACIServiceBase, IFC2LiveDLService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<FC2LiveDLService> _logger;

    public override string DownloaderName => "fc2livedl";

    public FC2LiveDLService(
        ILogger<FC2LiveDLService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string _,
                                                                                   string instanceName,
                                                                                   string channelId,
                                                                                   bool useCookiesFile = false,
                                                                                   CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/fc2-live-dl:2.1.3");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/fc2-live-dl:2.1.3");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --threads 1 -o '%(channel_id)s%(date)s%(time)s.%(ext)s' --log-level trace --cookies /fileshare/cookies/{channelId}.txt 'https://live.fc2.com/{channelId}/' && mv *.mp4 /fileshare/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --threads 1 -o '%(channel_id)s%(date)s%(time)s.%(ext)s' --log-level trace 'https://live.fc2.com/{channelId}/' && mv *.mp4 /fileshare/"
                };

            return CreateAzureContainerInstanceAsync(
                    template: "ACI.json",
                    parameters: new
                    {
                        dockerImageName = new
                        {
                            value = imageName
                        },
                        containerName = new
                        {
                            value = instanceName
                        },
                        commandOverrideArray = new
                        {
                            value = command
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
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}