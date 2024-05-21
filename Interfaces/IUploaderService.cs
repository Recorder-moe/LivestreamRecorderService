using LivestreamRecorderService.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IUploaderService
{
    string Image { get; }
    string ScriptName { get; }

    List<EnvironmentVariable> GetEnvironmentVariables();
}
