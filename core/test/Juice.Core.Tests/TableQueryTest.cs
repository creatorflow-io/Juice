using Juice.AspNetCore.Models;
using Xunit;

namespace Juice.Core.Tests
{
    public class TableQueryTest
    {
        [Fact]
        public void QueryShould()
        {
            var request = new TableQuery
            {
                Query = "  a b  c  ",
                Page = 2,
                PageSize = 100,
                Sorts = new[]
                {
                    new Sort
                    {
                        Property = "a",
                        Direction = SortDirection.Asc
                    },
                    new Sort
                    {
                        Property = "b",
                        Direction = SortDirection.Desc
                    }
                }
            };
            request.Standardizing();
            Assert.Equal("%a%b%c%", request.FilterText);
            Assert.Equal(2, request.Page);
            Assert.Equal(50, request.PageSize);
            Assert.Equal(50, request.SkipCount);
        }
    }
}
