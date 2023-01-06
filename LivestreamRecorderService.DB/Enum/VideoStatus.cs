namespace LivestreamRecorderService.DB.Enum;

public enum VideoStatus
{
    Scheduled,
    WaitingToRecord,
    Recording,
    Uploading,
    Archived,
    Expired,
    Removed
}
