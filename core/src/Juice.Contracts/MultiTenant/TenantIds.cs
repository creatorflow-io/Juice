namespace Juice.MultiTenant
{
    /// <summary>
    /// A tenant's Id: a GUID, written in its standard form (<c>0193f0aa-0000-7000-8000-0000000000a1</c>). This is what services store and
    /// pass (the tenant pattern decision record). It also reads the 26-character base32 form juice-tenants once issued
    /// (<c>StringIdGenerator.GenerateUniqueId</c>), which is the same 16 bytes, so an Id given in that form names the same tenant, and a
    /// store still keyed by it can be asked in it (<see cref="FormatBase32"/>) until it is rewritten.
    /// </summary>
    public static class TenantIds
    {
        private const string Base32Chars = "0123456789abcdefghjkmnpqrstvwxyz";
        private const int Base32Length = 26;

        /// <summary>The standard form of a tenant Id: 36 lowercase characters with hyphens.</summary>
        public static string Format(Guid id) => id.ToString("D");

        /// <summary>Reads a tenant Id in any GUID form, or in the base32 form; refuses anything else, including the empty GUID.</summary>
        public static bool TryParse(string? value, out Guid id)
        {
            id = Guid.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            value = value.Trim();
            if (Guid.TryParse(value, out id))
            {
                return id != Guid.Empty;
            }
            return TryParseBase32(value, out id) && id != Guid.Empty;
        }

        /// <summary>
        /// The standard form of a tenant Id given in either form, or null when it isn't one. For rewriting stored Ids to the standard form.
        /// </summary>
        public static string? Normalize(string? value) => TryParse(value, out var id) ? Format(id) : null;

        /// <summary>The old base32 form of an Id (as <c>StringIdGenerator.GenerateUniqueId(id)</c> writes it), for stores not yet rewritten.</summary>
        public static string FormatBase32(Guid id)
        {
            var bytes = id.ToByteArray();
            var chars = new char[Base32Length];
            for (var c = 0; c < Base32Length; c++)
            {
                var digit = 0;
                for (var i = 0; i < 5; i++)
                {
                    var bit = c * 5 + i;
                    digit <<= 1;
                    if (bit < 128 && (bytes[bit / 8] & (0x80 >> (bit % 8))) != 0)
                    {
                        digit |= 1;
                    }
                }
                chars[c] = Base32Chars[digit];
            }
            return new string(chars);
        }

        /// <summary>Whether the value is already the standard form (so it needs no rewrite).</summary>
        public static bool IsStandard(string? value) => value is not null && TryParse(value, out var id) && value == Format(id);

        // The reverse of StringIdGenerator.ToBase32: 5 bits per character, most significant first, 26 characters for 128 bits (the last
        // character's 2 low bits are padding and must be 0).
        private static bool TryParseBase32(string value, out Guid id)
        {
            id = Guid.Empty;
            if (value.Length != Base32Length)
            {
                return false;
            }
            var bytes = new byte[16];
            var bit = 0;
            foreach (var c in value)
            {
                var digit = Base32Chars.IndexOf(char.ToLowerInvariant(c));
                if (digit < 0)
                {
                    return false;
                }
                for (var i = 4; i >= 0; i--, bit++)
                {
                    var set = (digit >> i & 1) == 1;
                    if (bit >= 128)
                    {
                        if (set)
                        {
                            return false;
                        }
                        continue;
                    }
                    if (set)
                    {
                        bytes[bit / 8] |= (byte)(0x80 >> (bit % 8));
                    }
                }
            }
            id = new Guid(bytes);
            return true;
        }
    }
}
