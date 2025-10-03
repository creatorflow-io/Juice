using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Juice.Operation;
using Newtonsoft.Json.Linq;

namespace Juice
{
    public static class Validatable
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

        private static (string Name, string Kind) GetPropertyNameAndKind(IValidatable validatable, string name)
        {
            name = name.Trim('"');
            var kind = validatable.GetType().GetProperties().Any(p => p.Name == name) ? "Property" : "Argument";
            return (name, kind);
        }

        public static IValidatable NotExceededLength(this IValidatable validatable, string? value, int maxLength,
            [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value?.Length > maxLength)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" can be max {maxLength} characters long.");
            }
            return validatable;
        }

        public static IValidatable NotNullOrWhiteSpace(this IValidatable validatable, string? value, int? maxLength = default,
            [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

            if (string.IsNullOrWhiteSpace(value))
            {
                validatable.AddValidationError($"{kind} \"{name}\" can not be null or white space.");
            }
            if (maxLength.HasValue && value?.Length > maxLength)
            {
                validatable.AddValidationError($"{kind} \"{name}\" can be max {maxLength} characters long.");
            }
            return validatable;
        }

        public static IValidatable RegexMatch(this IValidatable validatable, string? value, string pattern, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value is null || !Regex.IsMatch(value, pattern))
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" does not match the required pattern.");
            }
            return validatable;
        }

        public static IValidatable NotNull(this IValidatable validatable, object? value, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value is null)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" can not be null.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, double value, double min, double max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, decimal value, decimal min, decimal max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, long value, long min, long max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, DateTime value, DateTime min, DateTime max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, DateTimeOffset value, DateTimeOffset min, DateTimeOffset max,
            [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable InRange(this IValidatable validatable, TimeSpan value, TimeSpan min, TimeSpan max, [CallerArgumentExpression(nameof(value))] string? property = null)
        {
            if (value < min || value > max)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
                validatable.AddValidationError($"{kind} \"{name}\" must be between {min} and {max}.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, int value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to 0.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, double value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to 0.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, decimal value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to 0.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, long value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to 0.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, TimeSpan value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < TimeSpan.Zero)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to 0.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, DateTime value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < DateTime.MinValue)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to {DateTime.MinValue}.");
            }
            return validatable;
        }

        public static IValidatable NotNegative(this IValidatable validatable, DateTimeOffset value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value < DateTimeOffset.MinValue)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than or equal to {DateTimeOffset.MinValue}.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, int value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than 0.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, double value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than 0.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, decimal value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than 0.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, long value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == 0)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than 0.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, TimeSpan value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == TimeSpan.Zero)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than 0.");
            }
            return validatable;
        }

        public static IValidatable NotZero(this IValidatable validatable, DateTime value, [CallerArgumentExpression("value")] string? property = null)
        {
            if (value == DateTime.MinValue)
            {
                var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));

                validatable.AddValidationError($"{kind} \"{name}\" must be greater than {DateTime.MinValue}.");
            }
            return validatable;
        }

        public static IValidatable ValidateJson(this IValidatable validatable, string? value, [CallerArgumentExpression("value")] string? property = null)
        {
            var (name, kind) = GetPropertyNameAndKind(validatable, property ?? nameof(value));
            if (value is null)
            {
                validatable.AddValidationError($"{kind} \"{name}\" can not be null.");
            }
            else
            {
                try
                {
                    var _ = JToken.Parse(value);
                }
                catch (Exception)
                {
                    validatable.AddValidationError($"{kind} \"{name}\" is not valid json.");
                }
            }
            return validatable;
        }

        public static void ThrowIfHasErrors(this IValidatable validatable)
        {
            if (validatable.ValidationErrors.Any())
            {
                throw new ValidationException(string.Join('\n', validatable.ValidationErrors));
            }
        }

        public static IOperationResult ValidationResult(this IValidatable validatable)
        {
            if (validatable.ValidationErrors.Any())
            {
                return new OperationResultInternal(OperationalFailure.InvalidArgument, string.Join('\n', validatable.ValidationErrors));
            }
            return OperationResult.Success;
        }
    }
}
