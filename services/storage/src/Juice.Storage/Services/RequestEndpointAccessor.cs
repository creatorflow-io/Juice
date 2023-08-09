namespace Juice.Storage.Services
{
    internal class RequestEndpointAccessor
    {
        public string Endpoint { get; private set; } = string.Empty;

        public void SetEndpoint(string endpoint)
        {
            Endpoint = endpoint;
        }
    }
}
