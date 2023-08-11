namespace Juice.Storage.Abstractions
{
    /// <summary>
    /// Determine how the web host should access the storage
    /// </summary>
    public class StorageEndpoint
    {
        public StorageEndpoint() { }
        public StorageEndpoint(string uri, string? basePath)
        {
            Uri = uri;
            BasePath = basePath;
        }
        public StorageEndpoint(string uri, string? basePath, string? identity, string? password, Protocol protocol) : this(uri, basePath)
        {
            Identity = identity;
            Password = password;
            Protocol = protocol;
        }
        public string? BasePath { get; private set; }
        public string Uri { get; private set; }
        public string? Identity { get; private set; }
        public string? Password { get; private set; }
        public Protocol Protocol { get; private set; }
    }
}
