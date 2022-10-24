namespace Juice.Utils
{
    public static class EnumUtils
    {
        public static T FromInt<T>(int value)
        {
            if (Enum.IsDefined(typeof(T), value))
            {
                return (T)Enum.ToObject(typeof(T), value);
            }
            return default(T);
        }
    }
}
