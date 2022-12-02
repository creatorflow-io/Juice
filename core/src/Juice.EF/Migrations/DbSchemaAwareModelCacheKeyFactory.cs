using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Juice.EF.Migrations
{
    public class DbSchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        /// <inheritdoc />
        public object Create(DbContext context)
        {
            return new { Type = context.GetType(), Schema = context is IDbContextSchema schema ? schema.Schema : null };
        }
    }
}
