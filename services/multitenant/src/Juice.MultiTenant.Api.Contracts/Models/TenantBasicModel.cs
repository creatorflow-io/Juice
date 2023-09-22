using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Contracts.Models
{
    public class TenantTableModel
    {
        public int Count { get; init; }
        public TenantBasicModel[] Data { get; init; }
    }
    public class TenantBasicModel
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Identifier { get; init; }
        public TenantStatus Status { get; init; }

        public TenantBasicModel()
        {
        }
        public TenantBasicModel(string id, string name, string identifier, TenantStatus status)
        {
            Id = id;
            Name = name;
            Identifier = identifier;
            Status = status;
        }
    }
}
