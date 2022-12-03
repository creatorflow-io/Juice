namespace Juice.MediatR.RequestManager.EF
{
    public enum RequestState
    {
        New = 0,
        Processed = 1,
        ProcessedFailed = 2
    }
}
