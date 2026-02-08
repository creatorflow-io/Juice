using Juice.Messaging.Policies;

namespace Juice.Messaging.Policies.Internal
{
    public static class PublishRuleMatchExtensions
    {
        internal static bool IsMatch(
            this PublishRuleMatch match,
            PolicyResolveContext context)
        {
            if (match == null) return false;
            if (context == null) return false;

            return MatchString(match.Event, context.EventType)
                && MatchString(match.Domain, context.Domain)
                && MatchString(match.TenantIdentifier, context.TenantIdentifier)
                && MatchString(match.TenantTier, context.TenantTier);
        }

        private static bool MatchString(string? ruleValue, string? contextValue)
        {
            // Rule does not constrain this field
            if (string.IsNullOrWhiteSpace(ruleValue))
                return true;

            // Rule expects a value but context doesn't have it
            if (string.IsNullOrWhiteSpace(contextValue))
                return false;

            return string.Equals(
                ruleValue,
                contextValue,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
