using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Contracts.Models
{
    public class TenantModel
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string? Identifier { get; init; }
        public string? ConnectionString { get; init; }
        public TenantStatus Status { get; init; }
        public string? OwnerUser { get; private set; }
        public string? OwnerName { get; private set; }
        public string SerializedProperties { get; init; }

        public TenantModel(string id, string name, string? identifier, string? connectionString,
            TenantStatus status, string? ownerUser, string properties)
            : this(id, name, identifier, connectionString, status, properties)
        {
            OwnerUser = ownerUser;
        }

        public TenantModel(string id, string name, string? identifier, string? connectionString,
            TenantStatus status, string properties)
        {
            Id = id;
            Name = name;
            Identifier = identifier;
            Status = status;
            SerializedProperties = properties;
            ConnectionString = connectionString;
        }

        public TenantModel SetOwnerName(string? ownerName)
        {
            OwnerName = ownerName;
            return this;
        }
    }
}
