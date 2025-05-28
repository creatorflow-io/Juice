using System.Runtime.CompilerServices;

namespace Juice
{
    public class Validator : IValidatable
    {
        public IList<string> ValidationErrors { get; } = [];

        #region Static Methods
        public static void ThrowIfNotExceededLength(string? value, int maxLength, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value?.Length > maxLength)
            {
                property ??= "value";
                throw new ArgumentOutOfRangeException(property, $"Property {property} can be max {maxLength} characters long.");
            }
        }

        public static void ThrowIfNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(value, property);
#else
            if (string.IsNullOrWhiteSpace(value))
            {
                property ??= "value";
                throw new ArgumentNullException(property, $"Argument \"{property}\" cannot be null or whitespace.");
            }
#endif
        }

        public static Validator New => new();
#endregion
    }

}
