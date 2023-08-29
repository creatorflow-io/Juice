using System.Security.Cryptography;
using System.Text;
using Juice.Storage.Abstractions;

namespace Juice.Storage.Extensions
{
    public static class StorageExtensions
    {

        /// <summary>
        /// Get MD5 of file and convert to HEX
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<string> GetMD5Async(this IStorage storage, string filePath, CancellationToken token)
        {
            using var md5 = MD5.Create();
            using var stream = await storage.ReadAsync(filePath, default);
            return ToHex(await md5.ComputeHashAsync(stream));
        }

        static string ToHex(byte[] bytes, bool upperCase = false)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            }

            return result.ToString();
        }

    }
}
