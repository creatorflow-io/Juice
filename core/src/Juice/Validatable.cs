using Juice.Operation;

namespace Juice
{
    public static partial class Validatable
    {
        public static IOperationResult ValidationResult(this IValidatable validatable)
        {
            if (validatable.ValidationErrors.Any())
            {
                return OperationResult.Failed(string.Join('\n', validatable.ValidationErrors), OperationalFailure.InvalidArgument);
            }
            return OperationResult.Success;
        }
    }
}
