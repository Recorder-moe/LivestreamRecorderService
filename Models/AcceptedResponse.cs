namespace LivestreamRecorderService.Models;

class AcceptedResponse
{
    public required string Id { get; set; }
    public required string StatusQueryGetUri { get; set; }
    public required string SendEventPostUri { get; set; }
    public required string TerminatePostUri { get; set; }
    public required string PurgeHistoryDeleteUri { get; set; }
}
