using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.EF
{
    internal class JsonConverter : ValueConverter<JObject, string>
    {
        public JsonConverter()
        : base(
            v => JsonConvert.SerializeObject(v ?? new JObject()),
            v => JsonConvert.DeserializeObject<JObject>(v ?? "{}"))
        {
        }
    }
}
