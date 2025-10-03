namespace Juice
{
    public interface IValidatable
    {
        IList<string> ValidationErrors { get; }
    }
}
