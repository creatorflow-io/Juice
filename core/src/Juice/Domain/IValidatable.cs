using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Domain
{
    public interface IValidatable
    {
        IList<string> ValidationErrors { get; }
    }

    public static class ValidatableExtensions
    {
        public static void AddValidationError(this IValidatable validatable, string error)
        {
            validatable.ValidationErrors.Add(error);
        }
        public static void ClearValidationErrors(this IValidatable validatable)
        {
            validatable.ValidationErrors.Clear();
        }

        public static string? TrimExceededLength(string? value, int maxLength)
        {
            if (value?.Length > maxLength)
            {
                return value.Substring(0, maxLength);
            }
            return value;
        }

        public static void NotExceededLength(this IValidatable validatable, string? value, int maxLength, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value?.Length > maxLength)
            {
                property ??= "value";
                validatable.AddValidationError($"Property {property} can be max {maxLength} characters long.");
            }
        }

        public static void NotNullOrWhiteSpace(this IValidatable validatable, string? value, int? maxLength = default, [CallerArgumentExpression("value")] string? property = null)
        {
            property ??= "value";
            if (string.IsNullOrWhiteSpace(value))
            {
                validatable.AddValidationError($"Property {property} can not be null or white space.");
            }
            if (maxLength.HasValue && value?.Length > maxLength)
            {
                validatable.AddValidationError($"Property {property} can be max {maxLength} characters long.");
            }
        }

        public static void ValidateJson(this IValidatable validatable, string? value, [CallerArgumentExpression("value")] string? property = null)
        {
            property ??= "value";
            if (value is null)
            {
                validatable.AddValidationError($"Property {property} can not be null.");
            }
            else
            {
                try
                {
                    JToken.Parse(value);
                }
                catch (JsonReaderException)
                {
                    validatable.AddValidationError($"Property {property} is not valid json.");
                }
            }
        }

        public static void ThrowIfHasErrors(this IValidatable validatable)
        {
            if (validatable.ValidationErrors.Any())
            {
                throw new ValidationException(string.Join('\n', validatable.ValidationErrors));
            }
        }
    }
}
