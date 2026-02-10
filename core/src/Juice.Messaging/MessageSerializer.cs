using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Juice.Messaging.Internal
{
    internal class MessageSerializer : IMessageSerializer
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<string> _allowedTypes = [];
        private readonly IEnumerable<string> _allowedAssemblies = ["Juice", "Juice."];
        public MessageSerializer(ILogger<MessageSerializer> logger)
        {
            _logger = logger;
        }
        public T? Deserialize<T>(string? payload, Type? eventType)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return default;
            }
            try
            {
                if (eventType == typeof(T))
                {
                    return JsonConvert.DeserializeObject<T>(payload);
                }

                if (eventType != null)
                {
                    if (!eventType.IsAssignableTo(typeof(T)))
                    {
                        throw new InvalidCastException($"Type {eventType.Name} can not be casted to {typeof(T).Name}");
                    }
                    return (T?)JsonConvert.DeserializeObject(payload, eventType);
                }

                // Handle object type
                if (typeof(T) == typeof(object))
                {
                    return (T)(object)JsonConvert.DeserializeObject<object>(payload)!;
                }

                // Handle value types
                if (typeof(T).IsValueType)
                {
                    return JsonConvert.DeserializeObject<T>(payload)!;
                }

                // Handle interfaces and abstract types
                if (typeof(T).IsInterface || typeof(T).IsAbstract)
                {
                    return DeserializeInterface<T>(payload);
                }

                // Handle concrete types
                return JsonConvert.DeserializeObject<T>(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize json for type {TypeName}. JSON: {Json}. {Message}",
                    typeof(T).Name,
                    payload, ex.Message);
                throw;
            }
        }

        public T? DeserializeFromUtf8Bytes<T>(byte[]? payload, Type? eventType)
        {
            var json = System.Text.Encoding.UTF8.GetString(payload ?? Array.Empty<byte>());
            return Deserialize<T>(json, eventType);
        }

        public string Serialize(object? value)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Formatting = Formatting.None
            };
            return JsonConvert.SerializeObject(value, settings);
        }

        public byte[] SerializeToUtf8Bytes(object? value)
            => System.Text.Encoding.UTF8.GetBytes(Serialize(value));

        private TResponse? DeserializeInterface<TResponse>(string json)
        {
            var settings = new JsonSerializerSettings
            {
                // match serialization so $type metadata is honored for concrete types
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
                SerializationBinder = new KnownTypesBinder(_allowedTypes, _allowedAssemblies),
                Converters = [new InterfaceConverter<TResponse>()]
            };

            return JsonConvert.DeserializeObject<TResponse>(json, settings);

        }

        /// <summary>
        /// Custom converter for handling interface deserialization
        /// </summary>
        private class InterfaceConverter<TInterface> : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType.IsInterface || objectType.IsAbstract;
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                // Read the JSON object
                var jObject = JObject.Load(reader);

                // Check for $type property which contains the actual type information
                var typeProperty = jObject["$type"];
                if (typeProperty != null)
                {
                    var typeName = typeProperty.Value<string>();
                    var actualType = Type.GetType(typeName!);

                    if (actualType != null && objectType.IsAssignableFrom(actualType))
                    {
                        return jObject.ToObject(actualType, serializer);
                    }
                }

                throw new JsonSerializationException(
                    $"Unable to deserialize interface type {objectType.Name}. " +
                    $"No $type property found and no concrete implementation could be determined.");
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                // Use default serialization
                serializer.Serialize(writer, value);
            }
        }

        /// <summary>
        /// Custom binder for known types to improve deserialization security
        /// </summary>
        private class KnownTypesBinder : ISerializationBinder
        {
            private readonly HashSet<string> _allowedTypes = [];
            private readonly HashSet<string> _allowedAssemblies = [];

            public KnownTypesBinder(IEnumerable<string>? allowedTypes, IEnumerable<string>? allowedAssemblies)
            {
                if (allowedTypes != null)
                {
                    foreach (var type in allowedTypes)
                    {
                        _allowedTypes.Add(type);
                    }
                }
                if (allowedAssemblies != null)
                {
                    foreach (var asm in allowedAssemblies)
                    {
                        _allowedAssemblies.Add(asm);
                    }
                }
            }
            public Type BindToType(string? assemblyName, string typeName)
            {
                // Add allowed type checking for security
                var fullTypeName = string.IsNullOrEmpty(assemblyName)
                    ? typeName
                    : $"{typeName}, {assemblyName}";

                // Security check: only allow whitelisted types
                if (!IsAllowedType(typeName, assemblyName))
                {
                    throw new JsonSerializationException(
                        $"Type {fullTypeName} is not in the allowed types list. " +
                        $"Deserialization blocked for security reasons.");
                }

                var type = Type.GetType(fullTypeName);

                if (type == null)
                {
                    throw new JsonSerializationException(
                        $"Type {fullTypeName} could not be resolved.");
                }

                return type;
            }

            private bool IsAllowedType(string typeName, string? assemblyName)
            {
                // Check exact type name
                if (_allowedTypes.Contains(typeName))
                {
                    return true;
                }

                // Check full type name with assembly
                var fullTypeName = string.IsNullOrEmpty(assemblyName)
                    ? typeName
                    : $"{typeName}, {assemblyName}";

                if (_allowedTypes.Contains(fullTypeName))
                {
                    return true;
                }

                // Allow types from trusted assemblies (optional)
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    // include the main Juice assembly name so internal concrete types can be resolved
                    if (_allowedAssemblies.Any(asm => asm == assemblyName || (asm.EndsWith(".") && assemblyName.StartsWith(asm))))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
            {
                assemblyName = serializedType.Assembly.GetName().Name;
                typeName = serializedType.FullName;
            }
        }
    }
}
