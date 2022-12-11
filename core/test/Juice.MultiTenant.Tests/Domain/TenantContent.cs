using System;
using Juice.Domain;

namespace Juice.MultiTenant.Tests.Domain
{
    public class TenantContent : DynamicEntity<Guid>
    {
        public TenantContent(string code, string name)
        {
            Name = name;
            Code = code;
        }

        public string Code { get; private set; }
        public string TenantId { get; private set; }

        public DateTimeOffset? ModifiedDate { get; protected set; }
    }
}
