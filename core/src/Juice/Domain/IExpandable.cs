namespace Juice.Domain
{
    public interface IExpandable
    {
        Dictionary<string, object?> OriginalPropertyValues { get; }
        Dictionary<string, object?> CurrentPropertyValues { get; }

    }
}
