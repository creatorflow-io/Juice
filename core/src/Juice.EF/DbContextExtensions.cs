using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public static class DbContextExtensions
    {
        /// <summary>
        /// Return new connection if database provider is SqlServer otherwise throw <see cref="NotSupportedException"/>.
        /// <para>The new connection is ReadOnly if DbContext has not changed else Read/Write</para>
        /// <para>NOTE: we don't return original connection because it can be close in dispose pattern so the current DbContext will throw <see cref="SqlException"/> after that.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IDbConnection CreateReadonlyConnection(this DbContextBase context)
        {
            if (context.Database.ProviderName == "SqlServer")
            {
                if (context.HasChanged)
                {
                    // return read/write connection to ensure read the updated data
                    return new SqlConnection(context.Database.GetConnectionString());
                }
                var builder = new SqlConnectionStringBuilder(context.Database.GetConnectionString());

                if (builder.ApplicationName == ".Net SqlClient Data Provider" && Assembly.GetEntryAssembly() != null)
                {
                    builder.ApplicationName = Assembly.GetEntryAssembly().FullName;
                }
                builder.ApplicationIntent = ApplicationIntent.ReadOnly;
                return new SqlConnection(builder.ConnectionString);
            }
            else
            {
                // we don't return original connection because it can be close in dispose pattern
                // so the current DbContext will be fail after that.
                //return context.Database.GetDbConnection();
                throw new NotSupportedException("Currently support SqlServer only.");
            }
        }
    }
}
