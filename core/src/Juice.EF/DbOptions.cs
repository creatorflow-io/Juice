using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class DbOptions
    {
        public string? DatabaseProvider { get; set; }
        public string? ConnectionName { get; set; }
        public string? Schema { get; set; }
        public JsonPropertyBehavior JsonPropertyBehavior { get; set; }
    }

    public class DbOptions<TContext> : DbOptions
        where TContext : DbContext
    {

    }

    public enum JsonPropertyBehavior
    {
        /// <summary>
        /// WARN: This option may decrease update/insert performance but safety for concurrency update
        /// <para>Update changed dynamic properties by execute raw sql.</para>
        /// <para>Apply for FIRST LEVEL of json properties. Ex:</para>
        /// <code>DynamicObj["DynamicField1"] = new {Time = DateTimeOffset.Now};
        /// DynamicObj["DynamicField2"] = 1;</code>
        /// </summary>
        UpdageCHANGES = 0,
        /// <summary>
        /// WARN: This option may increase update/insert performance but un-safety for concurrency update
        /// <para>Force write Properties object to store</para>
        /// </summary>
        UpdateALL = 1
    }
}
