using Microsoft.AspNetCore.Http;

namespace Juice.Extensions
{
    public static class PathStringExtensions
    {

        /// <summary>
        /// Get the path and id from the path string for route pattern with id is guid or number
        /// <para>/controller/{id}</para>
        /// <para>/controller/{id}/action</para>
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static (string path, string? id) GetPathComponents(this PathString path)
        {
            if (!path.HasValue)
            {
                return (string.Empty, null);
            }
            var paths = path.Value.Split('/');
            foreach (var p in paths)
            {
                if (Guid.TryParse(p, out var guid))
                {
                    return (path.Value.Replace($"/{p}", "/{id}"), p);
                }
                if (long.TryParse(p, out var number))
                {
                    return (path.Value.Replace($"/{p}", "/{id}"), p);
                }
            }
            return (path.Value, null);
        }
    }
}
