namespace Juice.MediatR.IdentifiedCommands.EF
{
    public enum RequestState
    {
        New = 0,
        Processed = 1,
        ProcessedFailed = 2
    }
}
