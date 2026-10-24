using Juice.Middlewares;

namespace Microsoft.AspNetCore.Builder
{
    public static class MessageContextApplicationBuilderExtensions
    {
        /// <summary>
        /// Initializes <see cref="Juice.Messaging.MessageContext"/> for every HTTP request (correlation id from the
        /// <c>x-correlation-id</c> header or a new one, <paramref name="source"/> as the context source) and clears it
        /// when the request ends. Register it before any endpoint that publishes through <c>IEventBus</c>.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="source">The source written into the context, usually the application name.</param>
        public static IApplicationBuilder UseMessageContext(this IApplicationBuilder app, string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("A message context source is required.", nameof(source));
            }
            return app.UseMiddleware<MessageContextMiddleware>(source);
        }
    }
}
