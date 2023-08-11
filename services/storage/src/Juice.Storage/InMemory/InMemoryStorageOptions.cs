using Juice.Storage.Abstractions;

namespace Juice.Storage.InMemory
{
    public class InMemoryStorageOptions
    {
        public bool RandomFilename { get; set; }
        public Storage[] Storages { get; set; }
    }

    public class Storage
    {
        public string WebBasePath { get; set; }
        public Endpoint[] Endpoints { get; set; }
    }
    public class Endpoint
    {
        public Protocol Protocol { get; set; }
        public string? BasePath { get; set; }
        public string Uri { get; set; }
        public string? Identity { get; set; }
        public string? Password { get; set; }

        public StorageEndpoint ToStorageEndpoint()
        {
            return new StorageEndpoint(Uri, BasePath, Identity, Password, Protocol);
        }
    }
}
