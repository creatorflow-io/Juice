namespace Juice.Messaging.Local
{
    /// <summary>
    /// Configuration options for the <c>"local-channel"</c> in-memory dispatch path.
    /// </summary>
    public class LocalChannelOptions
    {
        /// <summary>
        /// Maximum number of handlers executing simultaneously.
        /// <c>null</c> (default) means unlimited concurrent handlers.
        /// A positive integer <c>N</c> limits in-flight handler count to at most <c>N</c>
        /// via a <see cref="System.Threading.SemaphoreSlim"/>.
        /// </summary>
        public int? MaxConcurrency { get; set; }
    }
}
