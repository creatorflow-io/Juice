namespace Juice.Extensions.Options
{
    public interface IOptionsProvider<TService, TOptions>
        where TOptions : class
    {
        TOptions Value { get; }
    }
}
