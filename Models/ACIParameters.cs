// ReSharper disable InconsistentNaming

#pragma warning disable IDE1006

namespace LivestreamRecorderService.Models;

public record AciParameters(DockerImageName DockerImageName,
                            UploaderImageName UploaderImageName,
                            ContainerName ContainerName,
                            MountPath MountPath,
                            UploaderCommand UploaderCommand,
                            List<EnvironmentVariable> EnvironmentVariables,
                            CommandOverrideArray CommandOverrideArray);

public class DockerImageName(string value)
{
    public string value { get; set; } = value;
}

public record UploaderImageName(string value)
{
    public string value { get; set; } = value;
}

public record ContainerName(string value);

public record MountPath(string value = "/sharedvolume");

public record UploaderCommand(string[] value);

public class CommandOverrideArray(string[] value)
{
    public string[] value { get; set; } = [.. value];
}
