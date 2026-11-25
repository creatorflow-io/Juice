using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Juice.Messaging.Internal
{
    internal class MessageSerializer : IMessageSerializer
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<string> _allowedTypes = [];
        private readonly IEnumerable<string> _allowedAssemblies;
        // Rejected types already warned about: callers such as the local transport retry with a header-resolved type,
        // so one warning per type is enough to surface the configuration gap. The names come from the payload, so the
        // set is capped; later rejections are logged at Debug.
        private const int MaxWarnedTypes = 64;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _warnedTypes = new();
        // Dates are read with the offset they were written with. Newtonsoft's default (DateParseHandling.DateTime) turns
        // "...+00:00" into a local DateTime, so a DateTimeOffset came back in the host's offset. Settings passed here are
        // applied over JsonConvert.DefaultSettings, so a host's defaults still hold.
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        public MessageSerializer(ILogger<MessageSerializer> logger,
            IOptions<MessageSerializerOptions>? options = null)
        {
            _logger = logger;
            _allowedAssemblies = options?.Value.AllowedAssemblyPrefixes ?? ["Juice", "Juice."];
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
                    return JsonConvert.DeserializeObject<T>(payload, ReadSettings);
                }

                if (eventType != null)
                {
                    if (!eventType.IsAssignableTo(typeof(T)))
                    {
                        throw new InvalidCastException($"Type {eventType.Name} can not be casted to {typeof(T).Name}");
                    }
                    return (T?)JsonConvert.DeserializeObject(payload, eventType, ReadSettings);
                }

                // Handle object type
                if (typeof(T) == typeof(object))
                {
                    return (T)(object)JsonConvert.DeserializeObject<object>(payload, ReadSettings)!;
                }

                // Handle value types
                if (typeof(T).IsValueType)
                {
                    return JsonConvert.DeserializeObject<T>(payload, ReadSettings)!;
                }

                // Handle interfaces and abstract types
                if (typeof(T).IsInterface || typeof(T).IsAbstract)
                {
                    return DeserializeInterface<T>(payload);
                }

                // Handle concrete types
                return JsonConvert.DeserializeObject<T>(payload, ReadSettings);
            }
            catch (Exception ex)
            {
                if (FindBlockedType(ex) is { } blocked)
                {
                    LogBlockedType(ex, blocked);
                    throw;
                }
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
                DateParseHandling = DateParseHandling.DateTimeOffset,
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

        private void LogBlockedType(Exception ex, TypeNotAllowedException blocked)
        {
            if (_warnedTypes.Count >= MaxWarnedTypes || !_warnedTypes.TryAdd(blocked.TypeName + ", " + blocked.AssemblyName, 0))
            {
                _logger.LogDebug(ex, "Message type {TypeName} from assembly {AssemblyName} was rejected by the serializer allow-list.",
                    blocked.TypeName, blocked.AssemblyName);
                return;
            }
            // A type outside the allow-list is usually a configuration gap, not a corrupt payload: name the option that
            // fixes it. The advice is never derived from the rejected name, which comes from the payload.
            if (string.IsNullOrEmpty(blocked.AssemblyName))
            {
                _logger.LogWarning(ex,
                    "Message type {TypeName} was rejected: its $type has no assembly name, so it cannot match MessageSerializerOptions.AllowedAssemblyPrefixes. " +
                    "The sender must serialize with assembly names, as IMessageSerializer.Serialize does.",
                    blocked.TypeName);
                return;
            }
            _logger.LogWarning(ex,
                "Message type {TypeName} from assembly {AssemblyName} was rejected: the assembly is not in MessageSerializerOptions.AllowedAssemblyPrefixes ({AllowedPrefixes}). " +
                "If the type is yours, add the prefix of your own assemblies, e.g. services.Configure<MessageSerializerOptions>(o => o.AllowedAssemblyPrefixes.Add(\"MyCompany.\")) " +
                "or AddMessaging().ConfigureSerializer(...). Never add a framework prefix such as \"System.\": it re-enables unsafe type deserialization.",
                blocked.TypeName, blocked.AssemblyName, string.Join(", ", _allowedAssemblies));
        }

        private static TypeNotAllowedException? FindBlockedType(Exception? ex)
        {
            for (; ex != null; ex = ex.InnerException)
            {
                if (ex is TypeNotAllowedException blocked)
                {
                    return blocked;
                }
            }
            return null;
        }

        /// <summary>
        /// Thrown by <see cref="KnownTypesBinder"/> when <c>$type</c> names an assembly outside
        /// <see cref="MessageSerializerOptions.AllowedAssemblyPrefixes"/>.
        /// </summary>
        internal sealed class TypeNotAllowedException : JsonSerializationException
        {
            public TypeNotAllowedException(string typeName, string? assemblyName)
                : base($"Type {typeName}{(string.IsNullOrEmpty(assemblyName) ? "" : ", " + assemblyName)} is not in the allowed types list. " +
                      "Deserialization blocked for security reasons. If the type is yours, add your assembly prefix to MessageSerializerOptions.AllowedAssemblyPrefixes.")
            {
                TypeName = typeName;
                AssemblyName = assemblyName;
            }

            public string TypeName { get; }
            public string? AssemblyName { get; }
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
                    throw new TypeNotAllowedException(typeName, assemblyName);
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
                    if (FrameworkAssemblies.Contains(assemblyName))
                    {
                        return IsAllowedFrameworkType(typeName);
                    }
                }

                return false;
            }

            // A message's List<T> or Dictionary<string, T> is written with the BCL's assembly name, so the prefixes alone reject it
            // even when T is allowed. A framework type is allowed only when it is a plain value (a string, a number, a Guid, ...),
            // an array of one, or one of these collections, and every type argument is allowed in its own right. Nothing else from
            // the framework is: those are the types an unsafe payload would name.
            private static readonly HashSet<string> FrameworkAssemblies =
            [
                "System.Private.CoreLib", "mscorlib", "netstandard", "System.Runtime",
                "System.Collections", "System.Collections.Concurrent", "System.ObjectModel"
            ];

            private static readonly HashSet<string> FrameworkCollections =
            [
                "System.Collections.Generic.List`1",
                "System.Collections.Generic.Dictionary`2",
                "System.Collections.Generic.HashSet`1",
                "System.Collections.Generic.SortedDictionary`2",
                "System.Collections.Generic.SortedList`2",
                "System.Collections.Generic.SortedSet`1",
                "System.Collections.Generic.LinkedList`1",
                "System.Collections.Generic.Queue`1",
                "System.Collections.Generic.Stack`1",
                "System.Collections.Generic.KeyValuePair`2",
                "System.Collections.ObjectModel.Collection`1",
                "System.Collections.ObjectModel.ReadOnlyCollection`1",
                "System.Collections.ObjectModel.ObservableCollection`1",
                "System.Collections.ObjectModel.ReadOnlyDictionary`2",
                "System.Collections.Concurrent.ConcurrentDictionary`2",
                "System.Collections.Concurrent.ConcurrentBag`1",
                "System.Collections.Concurrent.ConcurrentQueue`1",
                "System.Nullable`1"
            ];

            private static readonly HashSet<string> FrameworkValues =
            [
                "System.Object", "System.String", "System.Boolean", "System.Char",
                "System.Byte", "System.SByte", "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
                "System.Int64", "System.UInt64", "System.Single", "System.Double", "System.Decimal",
                "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.DateOnly", "System.TimeOnly", "System.Guid"
            ];

            private bool IsAllowedFrameworkType(string typeName)
            {
                var open = typeName.IndexOf('[');
                if (open < 0)
                {
                    return FrameworkValues.Contains(typeName);
                }
                // An array: "System.String[]", "System.Int32[,]"
                if (typeName.EndsWith("]") && !typeName.Contains("[["))
                {
                    return FrameworkValues.Contains(typeName[..open]) && typeName[open..].All(c => c is '[' or ']' or ',');
                }
                // A generic: "System.Collections.Generic.List`1[[Name, Assembly, Version=...]]"
                if (!FrameworkCollections.Contains(typeName[..open]) || !typeName.EndsWith("]"))
                {
                    return false;
                }
                var arguments = SplitTypeArguments(typeName[(open + 1)..^1]);
                return arguments != null && arguments.Count > 0 && arguments.All(argument =>
                {
                    var (argumentType, argumentAssembly) = SplitAssemblyQualifiedName(argument);
                    return IsAllowedType(argumentType, argumentAssembly);
                });
            }

            /// <summary>Splits "[A, a],[B, b]" into "A, a" and "B, b"; null when the brackets don't balance.</summary>
            private static List<string>? SplitTypeArguments(string arguments)
            {
                var result = new List<string>();
                var depth = 0;
                var start = -1;
                for (var i = 0; i < arguments.Length; i++)
                {
                    switch (arguments[i])
                    {
                        case '[':
                            if (depth++ == 0)
                            {
                                start = i + 1;
                            }
                            break;
                        case ']':
                            if (--depth < 0)
                            {
                                return null;
                            }
                            if (depth == 0)
                            {
                                result.Add(arguments[start..i]);
                            }
                            break;
                        case ',' when depth == 0:
                        case ' ' when depth == 0:
                            break;
                        default:
                            if (depth == 0)
                            {
                                return null;
                            }
                            break;
                    }
                }
                return depth == 0 ? result : null;
            }

            /// <summary>Splits "Ns.Type`1[[...]], Assembly, Version=..." at the first comma outside brackets.</summary>
            private static (string TypeName, string? AssemblyName) SplitAssemblyQualifiedName(string name)
            {
                var depth = 0;
                for (var i = 0; i < name.Length; i++)
                {
                    switch (name[i])
                    {
                        case '[': depth++; break;
                        case ']': depth--; break;
                        case ',' when depth == 0:
                            var assembly = name[(i + 1)..].Split(',')[0].Trim();
                            return (name[..i].Trim(), assembly.Length == 0 ? null : assembly);
                    }
                }
                return (name.Trim(), null);
            }

            public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
            {
                assemblyName = serializedType.Assembly.GetName().Name;
                typeName = serializedType.FullName;
            }
        }
    }
}
