using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Juice.Domain
{
    public interface IExpandable: IDynamic
    {
        [NotMapped]
        [JsonIgnore]
        Dictionary<string, JToken?> OriginalPropertyValues { get; }

        [NotMapped]
        [JsonIgnore]
        Dictionary<string, JToken?> CurrentPropertyValues { get; }
        JObject Properties { get; }
    }
}
