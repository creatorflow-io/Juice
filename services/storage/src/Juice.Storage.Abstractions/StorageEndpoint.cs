namespace Juice.Storage.Abstractions
{
    public class StorageEndpoint
    {
        public StorageEndpoint() { }
        public StorageEndpoint(string uri, string? basePath)
        {
            Uri = uri;
            BasePath = basePath;
        }
        public StorageEndpoint(string uri, string? basePath, string? identity, string? password) : this(uri, basePath)
        {
            Identity = identity;
            Password = password;
        }
        public string? BasePath { get; private set; }
        public string Uri { get; private set; }
        public string? Identity { get; private set; }
        public string? Password { get; private set; }
    }
}
