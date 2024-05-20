using LivestreamRecorderService.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IUploaderService
{
    string Image { get; }

    List<EnvironmentVariable> GetEnvironmentVariables();
}
