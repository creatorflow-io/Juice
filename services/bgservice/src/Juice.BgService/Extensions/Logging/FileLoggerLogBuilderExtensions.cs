using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Extensions.Logging
{
    public static class FileLoggerLogBuilderExtensions
    {
        public static void AddBgServiceFileLogger(this ILoggingBuilder builder, IConfigurationSection configuration)
        {
            builder.Services.Configure<FileLoggerOptions>(configuration);
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());
        }
    }
}
