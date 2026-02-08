
namespace Juice.Messaging.Context
{
    public sealed record MessageContextData(
        string CorrelationId,
        string? CausationId,
        string ExecutionId,
        string Source
    );
}
