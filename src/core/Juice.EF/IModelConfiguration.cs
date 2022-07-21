using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public interface IModelConfiguration
    {
        void OnModelCreating(ModelBuilder builder);
    }
}
