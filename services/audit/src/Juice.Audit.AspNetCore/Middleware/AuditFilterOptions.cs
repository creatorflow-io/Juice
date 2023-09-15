namespace Juice.Audit.AspNetCore.Middleware
{
    public class AuditFilterOptions
    {
        public AuditFilterEntry[] Filters { get; set; } = Array.Empty<AuditFilterEntry>();

        public void AddFilter(string prefix, params string[] methods)
        {
            var newFilters = new List<AuditFilterEntry>(Filters)
            {
                new AuditFilterEntry
                {
                    Prefix = prefix,
                    Methods = methods
                }
            };
            Filters = newFilters.ToArray();
        }

        public bool IsMatch(string path, string method)
        {
            if (Filters.Length == 0) return true;

            foreach (var filter in Filters)
            {
                if (filter.IsMatch(path, method))
                {
                    if (!filter.IsGlobal)
                    {
                        return true;
                    }
                    else
                    {
                        return !Filters.Any(f => f.IsExcept(path, method));
                    }
                }
            }
            return false;
        }
    }

    public class AuditFilterEntry
    {
        public string Prefix { get; set; } = string.Empty;
        public string[] Methods { get; set; } = Array.Empty<string>();
        public bool IsGlobal => Prefix == string.Empty || Prefix == "*";
        public bool IsMatch(string path, string method)
        {
            return (IsGlobal || path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                && (Methods.Length == 0 || Methods.Contains(method, new StringComparer()));
        }
        public bool IsExcept(string path, string method)
        {
            return !IsGlobal && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && Methods.Length > 0 && !Methods.Contains(method, new StringComparer());
        }
    }

    internal class StringComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }
        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }
}
