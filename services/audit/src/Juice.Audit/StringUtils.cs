using System.Text.RegularExpressions;

namespace Juice.Audit
{
    public static class StringUtils
    {
        public static bool IsHeaderMatch(string header, string pattern)
        {
            return Regex.IsMatch(header,
               "^" + pattern
                            .Replace("*", "([^-]+){1}")
                            .Replace("-#", "(-[^-]+)*")
                            .Replace("#-", "([^-]+-)*") + "$", RegexOptions.IgnoreCase);
        }

        public static bool IsPathMatch(string path, string expected)
        {
            return Regex.IsMatch(path,
               "^" + expected
                            .Replace("*", "([^\\/]+){1}")
                            .Replace("/#", "(\\/[^\\/]+)*")
                            .Replace("#/", "([^\\/]+\\/)*") + "$", RegexOptions.IgnoreCase);
        }
    }
}
