using Microsoft.Extensions.Localization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Juice.Converters
{
    /// <summary>
    /// Serializes the <see cref="LocalizedString"/> to a simple string using the translated text.
    /// </summary>
    public class LocalizedStringConverter : JsonConverter<LocalizedString>
    {
        public override LocalizedString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();
        public override void Write(Utf8JsonWriter writer, LocalizedString value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
