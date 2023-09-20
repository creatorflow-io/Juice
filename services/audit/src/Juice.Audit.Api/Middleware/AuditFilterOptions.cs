namespace Juice.Audit.AspNetCore.Middleware
{
    public class AuditFilterOptions
    {
        public PathFilterEntry[] Filters { get; set; } = Array.Empty<PathFilterEntry>();

        public AuditFilterOptions Clear()
        {
            Filters = Array.Empty<PathFilterEntry>();
            return this;
        }

        public AuditFilterOptions Include(string path, params string[] methods)
        {
            var newFilters = new List<PathFilterEntry>(Filters)
            {
                new PathFilterEntry
                {
                    Path = path,
                    Methods = methods,
                    Priority = Filters.Length
                }
            };
            Filters = newFilters.ToArray();
            return this;
        }

        public AuditFilterOptions Exclude(string path, params string[] methods)
        {
            var newFilters = new List<PathFilterEntry>(Filters)
            {
                new PathFilterEntry
                {
                    Path = path,
                    Methods = methods,
                    Priority = Filters.Length,
                    IsExcluded = true
                }
            };
            Filters = newFilters.ToArray();
            return this;
        }

        public bool IsMatch(string path, string method)
            => IsMatch(path, method, out var _);
        public bool IsMatch(string path, string method, out string? rule)
        {
            if (Filters.Length == 0)
            {
                rule = null;
                return true;
            }

            foreach (var filter in Filters.OrderByDescending(f => f.Priority))
            {
                if (filter.IsMatch(path, method))
                {
                    rule = filter.Path;
                    return !filter.IsExcluded;
                }
            }
            rule = null;
            return false;
        }

        public string[] ReqHeaders = new string[] {
            ":authority:",
            "accept-#",
            "content-*",
            "x-forwarded-#",
            "referer",
            "user-agent"
        };

        public string[] ResHeaders = new string[]
        {
            "content-*"
        };

        public AuditFilterOptions StoreEmptyRequestHeaders()
        {
            ReqHeaders = Array.Empty<string>();
            return this;
        }

        public AuditFilterOptions StoreRequestHeaders(params string[] headers)
        {
            var newHeaders = new List<string>(ReqHeaders);
            newHeaders.AddRange(headers);
            ReqHeaders = newHeaders.ToArray();
            return this;
        }

        public AuditFilterOptions StoreEmptyResponseHeaders()
        {
            ResHeaders = Array.Empty<string>();
            return this;
        }

        public AuditFilterOptions StoreResponseHeaders(params string[] headers)
        {
            var newHeaders = new List<string>(ResHeaders);
            newHeaders.AddRange(headers);
            ResHeaders = newHeaders.ToArray();
            return this;
        }


        public bool IsReqHeaderMatch(string header)
        {
            return ReqHeaders.Any(h =>
                StringUtils.IsHeaderMatch(header, h));
        }

        public bool IsResHeaderMatch(string header)
        {
            return ResHeaders.Any(h =>
                StringUtils.IsHeaderMatch(header, h));
        }

    }

    public class PathFilterEntry
    {
        public int Priority { get; set; } = 0;
        public bool IsExcluded { get; set; } = false;
        public string Path { get; set; } = string.Empty;
        public string[] Methods { get; set; } = Array.Empty<string>();
        public bool IsGlobal => Path == string.Empty;
        public bool IsMatch(string path, string method)
        {
            return (IsGlobal || StringUtils.IsPathMatch(path, Path))
                && (Methods.Length == 0 || Methods.Contains(method, new StringComparer()));
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
