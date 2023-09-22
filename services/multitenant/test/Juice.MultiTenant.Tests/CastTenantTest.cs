using Finbuckle.MultiTenant;
using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Xunit;

namespace Juice.MultiTenant.Tests
{
    public class CastTenantTest
    {
        [Fact]
        public void Test()
        {
            var t = Get<TenantInfo>();
            Assert.NotNull(t);
            var t1 = Get<Tenant>();
            Assert.Null(t1);
        }

        private T? Get<T>()
            where T : class, ITenantInfo, new()
        {
            return new TenantInfo() as T;
        }
    }
}
