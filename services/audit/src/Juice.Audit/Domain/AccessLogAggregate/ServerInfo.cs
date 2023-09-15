using Juice.Domain;

namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class ServerInfo : ValueObject
    {
        public string MachineName { get; init; }
        public string OSVersion { get; init; }
        public string? SoftwareVersion { get; init; }
        public string AppName { get; init; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return MachineName;
        }
    }
}
