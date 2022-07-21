using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class ModelConfigurationBase<T> : IModelConfiguration
        where T : DbContext
    {
        public abstract void OnModelCreating(ModelBuilder builder);
    }
}
