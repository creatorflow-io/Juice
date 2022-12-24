namespace Juice.Workflows.Models
{
    /// <summary>
    /// Halt workflow and wait till resume signal
    /// </summary>
    public record BlockingNode(string Id, string Name);

    /// <summary>
    /// Node was executed and finished
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="Message"></param>
    /// <param name="User"></param>
    /// <param name="Outcomes"></param>
    public record ExecutedNode(string Id, string? Message, string? User, IEnumerable<string>? Outcomes);

    public record FaultedNode(string Id, string? Message, string? User);

    /// <summary>
    /// Waitting for all inbound flows are completed.
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="Name"></param>
    public record IdlingNode(string Id, string Name);

    public record ListeningEvent(string Id, string Name);
}
