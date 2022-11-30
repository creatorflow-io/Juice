using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class DbOptions
    {
        public string? DatabaseProvider { get; set; }
        public string? ConnectionName { get; set; }
        public string? Schema { get; set; }
    }

    public class DbOptions<TContext> : DbOptions
        where TContext : DbContext
    {

    }
}
