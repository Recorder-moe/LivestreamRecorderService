namespace LivestreamRecorder.DB.Models;

public class Timestamps
{
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
}

