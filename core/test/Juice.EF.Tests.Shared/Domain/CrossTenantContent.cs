using System;
using Juice.Domain;

namespace Juice.EF.Tests.Domain
{
    public class CrossTenantContent : Entity<Guid>
    {
        public CrossTenantContent(Guid id, string name) : base(id, name)
        {
        }
    }
}
