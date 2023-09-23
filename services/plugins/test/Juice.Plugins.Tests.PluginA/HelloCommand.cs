using Juice.Plugins.Tests.Common;
using Juice.Plugins.Tests.PluginBase;
using Juice.Services;
using Microsoft.Extensions.Logging;

namespace Juice.Plugins.Tests.PluginA
{
    public class HelloCommand : ICommand
    {
        public string Name { get => "hello"; }
        public string Description { get => "Displays hello message."; }

        private MessageService _message;
        private ILogger _logger;
        private SharedService _sharedService;
        public HelloCommand(MessageService message, ILogger<HelloCommand> logger, SharedService sharedService)
        {
            _message = message;
            _logger = logger;
            _sharedService = sharedService;
        }

        public string Execute()
        {
            _logger.LogInformation(_message.Hello() + " " + _sharedService.Id);
            return _message.Hello() + " " + new DefaultStringIdGenerator().GenerateRandomId(6) + " " + _sharedService.Id;
        }

        public void Dispose()
        {
            _message = null!;
        }
    }
}
