namespace Juice.Services
{
    public interface IStringIdGenerator
    {
        /// <summary>
        /// Generate an Base32 unique string based on GUID
        /// </summary>
        /// <returns></returns>
        string GenerateUniqueId();

        /// <summary>
        /// Generate a random string with specified length
        /// </summary>
        /// <param name="length"></param>
        /// <param name="caseSenitive"></param>
        /// <returns></returns>
        string GenerateRandomId(uint length, bool caseSenitive = false);
    }
}
