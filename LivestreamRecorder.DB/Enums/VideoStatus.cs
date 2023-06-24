namespace LivestreamRecorder.DB.Enums;

public enum VideoStatus
{
    Unknown = -1,

    Scheduled = 0,
    Pending = 10,

    WaitingToRecord = 11,
    WaitingToDownload = 12,

    Recording = 20,
    Downloading = 21,

    Uploading = 30,

    Archived = 40,
    PermanentArchived = 41,

    Expired = 50,
    Skipped = 51,
    Missing = 52,
    Error = 53,
    Reject = 54,

    Exist = 60,
    Edited = 61,
    Deleted = 62
}
