namespace Juice.Messaging
{
    /// <summary>
    /// Configuration options for <see cref="IMessageSerializer"/> deserialization security.
    /// Controls which assemblies are allowed when resolving <c>$type</c> metadata during
    /// message deserialization.
    /// <para>
    /// By default, only <c>Juice.*</c> assemblies are allowed. Application assemblies
    /// that contain event types must be explicitly registered via
    /// <see cref="AllowedAssemblyPrefixes"/> to enable deserialization by the local
    /// transport publisher and other paths that rely on <c>$type</c> resolution.
    /// </para>
    /// </summary>
    public class MessageSerializerOptions
    {
        /// <summary>
        /// Assembly name prefixes allowed for <c>$type</c> deserialization.
        /// An assembly is allowed if its name equals a prefix exactly or starts with
        /// a prefix that ends with <c>"."</c>.
        /// <para>
        /// Defaults: <c>["Juice", "Juice."]</c>
        /// </para>
        /// <example>
        /// <code>
        /// options.AllowedAssemblyPrefixes.Add("MyApp");
        /// options.AllowedAssemblyPrefixes.Add("MyCompany.");
        /// </code>
        /// </example>
        /// </summary>
        public List<string> AllowedAssemblyPrefixes { get; set; } = ["Juice", "Juice."];
    }
}
