namespace Juice.Storage.Middleware
{
    public class StorageMiddlewareOptions
    {
        public string[] Endpoints { get; set; }
        public bool SupportDownloadByPath { get; set; }
    }
}
