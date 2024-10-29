using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

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
