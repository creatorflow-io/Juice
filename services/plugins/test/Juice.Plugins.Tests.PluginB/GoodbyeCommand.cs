using Juice.Plugins.Tests.Common;
using Juice.Plugins.Tests.PluginBase;

namespace Juice.Plugins.Tests.PluginB
{
    public class GoodbyeCommand : ICommand
    {
        public string Name => "bye";

        public string Description => "Displays goodbye message.";

        private MessageService _message;
        private SharedService _sharedService;

        public GoodbyeCommand(MessageService message, SharedService sharedService)
        {
            _message = message;
            _sharedService = sharedService;
        }

        public void Dispose()
        {
            _message = null!;
        }
        public string Execute() => _message.Goodbye() + " " + _sharedService.Id;
    }
}
