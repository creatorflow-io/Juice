using System;

namespace Juice.Messaging.Attributes
{
    /// <summary>
    /// Specifies the domain context for an event or message.
    /// This attribute is used by the Outbox pattern to route messages to the correct publishers.
    /// </summary>
    /// <example>
    /// <code>
    /// [Domain("Orders")]
    /// public record OrderCreatedIntegrationEvent : IntegrationEvent
    /// {
    ///     public string OrderId { get; init; }
    /// }
    /// 
    /// [Domain("Content", Subdomain = "CMS")]
    /// public record ContentPublishedIntegrationEvent : IntegrationEvent
    /// {
    ///     public string ContentId { get; init; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class DomainAttribute : Attribute
    {
        /// <summary>
        /// The primary domain name (e.g., "Orders", "Content", "Inventory")
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Optional subdomain for more granular routing (e.g., "CMS", "Fulfillment")
        /// </summary>
        public string? Subdomain { get; set; }

        /// <summary>
        /// Optional category for event classification (e.g., "Command", "Query", "Notification")
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Optional tags for additional metadata (comma-separated)
        /// </summary>
        public string? Tags { get; set; }

        public DomainAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Domain name cannot be null or empty.", nameof(name));
            
            Name = name;
        }

        /// <summary>
        /// Gets the full domain identifier (e.g., "Orders.Fulfillment" or "Content")
        /// </summary>
        public string GetFullDomain()
        {
            return string.IsNullOrWhiteSpace(Subdomain) 
                ? Name 
                : $"{Name}.{Subdomain}";
        }

        /// <summary>
        /// Gets all tags as an array
        /// </summary>
        public string[] GetTags()
        {
            return string.IsNullOrWhiteSpace(Tags)
                ? Array.Empty<string>()
                : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
