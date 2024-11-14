using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF.Tests.Infrastructure
{
    internal class ContentRepository : RepositoryBase<Content, TestContext>
    {
        public ContentRepository(TestContext context) : base(context)
        {
        }

        public async Task TestDbContextAsync()
        {
            _ = await DbContext.Set<Content>().CountAsync();
        }
    }
}
