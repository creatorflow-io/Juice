namespace Juice.Storage.Middleware
{
    public class StorageMiddlewareOptions
    {
        public string[] Endpoints { get; set; }
        public bool WriteOnly { get; set; }
    }
}
