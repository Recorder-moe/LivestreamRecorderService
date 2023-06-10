using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models;

public struct FileInfo
{
    [Required]
    public required string Name { get; set; }
    public long? FileSize { get; set; }
}
