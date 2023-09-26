using Microsoft.Extensions.Logging;

namespace Juice.BgService
{
    public abstract class BackgroundService : ManagedService, IManagedService<IServiceModel>
    {

        protected IDisposable? _logScope;
        protected ILogger _logger;
        public BackgroundService(ILogger logger) : base()
        {
            _logger = logger;
        }

        public override void SetDescription(string description)
        {
            base.SetDescription(description);
            InitLogScope();
        }

        #region Logging

        protected virtual List<KeyValuePair<string, object>> CreateLogScope()
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("ServiceId", Id),
                new KeyValuePair<string, object>("ServiceType", GetType()?.FullName ?? ""),
                new KeyValuePair<string, object>("ServiceDescription", Description)
            };
        }

        protected virtual void InitLogScope()
        {
            _logScope?.Dispose();
            _logScope = _logger.BeginScope(CreateLogScope());
        }

        public virtual void Logging(string message, LogLevel level = LogLevel.Information)
        {
            if (_logger.IsEnabled(level))
            {
                _logger.Log(level, message);
            }
        }

        public abstract void Configure(IServiceModel model);
        #endregion

    }

}
