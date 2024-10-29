using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json.Linq;

namespace Juice.EF
{
    internal class JsonConverter : ValueConverter<JObject, string>
    {
        public JsonConverter()
        : base(
            v => v.ToString(),
            v => JObject.Parse(v))
        {
        }
    }
}
