using System.Text;

namespace Juice.Services
{
    public class DefaultStringIdGenerator : IStringIdGenerator
    {
        // Some confusing chars are ignored: http://www.crockford.com/wrmg/base32.html
        private const string _encode32Chars = "0123456789abcdefghjkmnpqrstvwxyz";
        private const string _encode32CaseSensitiveChars = "0123456789AaBbCcDdEeFfGgHhJjKkMmNnPpQqRrSsTtVvWwXxYyZz";
        public string GenerateUniqueId(Guid? id = default)
        {
            // Generate a base32 Guid value
            var guid = (id ?? Guid.NewGuid()).ToByteArray();
            return ToBase32(guid);
        }

        private static string ToBase32(byte[] bytes)
        {
            var output = "";
            for (var bitIndex = 0; bitIndex < bytes.Length * 8; bitIndex += 5)
            {
                var dualbyte = bytes[bitIndex / 8] << 8;
                if (bitIndex / 8 + 1 < bytes.Length)
                {
                    dualbyte |= bytes[bitIndex / 8 + 1];
                }
                dualbyte = 0x1f & (dualbyte >> (16 - bitIndex % 8 - 5));
                output += _encode32Chars[dualbyte];
            }

            return output;
        }

        public string GenerateRandomId(uint length, bool caseSenitive = false)
            => RandomString(length, caseSenitive);
        private static string RandomString(uint length, bool caseSenitive)
        {
            var sb = new StringBuilder();
            var random = new Random();
            var chars = caseSenitive ? _encode32CaseSensitiveChars : _encode32Chars?.ToUpper();
            while (length-- > 0)
            {
                var num = random.Next(chars.Length);
                sb.Append(chars[num]);
            }
            return sb.ToString();
        }
    }
}
