namespace Juice
{
    public static class Validator
    {
        public static void NotNullOrWhiteSpace(string? value, string property, int? maxLength = default)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(property);
            }
            if (maxLength.HasValue && value.Length > maxLength)
            {
                throw new ArgumentOutOfRangeException(property, $"Property {property} can be max {maxLength} characters long.");
            }
        }
    }
}
