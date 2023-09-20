using System.Runtime.CompilerServices;

namespace Juice
{
    public static class Validator
    {
        public static void NotNullOrWhiteSpace(string? value, [CallerArgumentExpression("value")] string? property = null, int? maxLength = default)
        {
            property ??= "value";
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(property);
            }
            if (maxLength.HasValue && value.Length > maxLength)
            {
                throw new ArgumentOutOfRangeException(property, $"Property {property} can be max {maxLength} characters long.");
            }
        }

        public static void NotExceededLength(string? value, int maxLength, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value?.Length > maxLength)
            {
                property ??= "value";
                throw new ArgumentOutOfRangeException(property, $"Property {property} can be max {maxLength} characters long.");
            }
        }
    }
}
