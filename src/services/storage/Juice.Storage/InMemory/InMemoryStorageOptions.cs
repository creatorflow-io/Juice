using Juice.Storage.Abstractions;

namespace Juice.Storage.InMemory
{
    public class InMemoryStorageOptions
    {
        public bool RandomFilename { get; set; }
        public string StorageProvider { get; set; }
        public Endpoint Endpoint { get; set; }
    }

    public class Endpoint
    {
        public string? BasePath { get; set; }
        public string Uri { get; set; }
        public string? Identity { get; set; }
        public string? Password { get; set; }

        public StorageEndpoint ToStorageEndpoint()
        {
            return new StorageEndpoint(Uri, BasePath, Identity, Password);
        }
    }
}
