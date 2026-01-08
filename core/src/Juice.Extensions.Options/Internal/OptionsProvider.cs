namespace Juice.Extensions.Options.Internal
{
    internal sealed class OptionsProvider<TService, TOptions>
        : IOptionsProvider<TService, TOptions>
        where TOptions : class
    {
        private readonly TOptions _options;

        public OptionsProvider(TOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public TOptions Value => _options;
    }
}
