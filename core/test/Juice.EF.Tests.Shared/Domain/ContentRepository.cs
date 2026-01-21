using Juice.EF.Tests.Infrastructure;

namespace Juice.EF.Tests.Domain
{
    internal class ContentRepository : RepositoryBase<Content, TestContext>
    {
        public ContentRepository(TestContext context) : base(context)
        {
        }
    }
}
