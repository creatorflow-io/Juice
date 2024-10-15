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
        public static Validator New => new();
        #endregion
    }

}
