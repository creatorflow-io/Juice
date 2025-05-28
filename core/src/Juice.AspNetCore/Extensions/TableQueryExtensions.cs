using Juice.AspNetCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Juice.Extensions
{
    public static class TableQueryExtensions
    {
        private static IQueryable<TSource> ApplyQuery<TSource>(
            IQueryable<TSource> query, DatasourceRequest request)
        {
            foreach (var sort in request.Sorts)
            {
                if (string.IsNullOrEmpty(sort.Property))
                {
                    continue;
                }
                var property = string.Concat(sort.Property[0].ToString().ToUpper(), sort.Property.AsSpan(1));

                if (sort.Direction == SortDirection.Asc)
                {
                    query = query is IOrderedQueryable<TSource> ordered
                        && query.Expression.Type == typeof(IOrderedQueryable<TSource>)
                        ? ordered.ThenBy(property)
                        : query.OrderBy(property);
                }
                else
                {
                    query = query is IOrderedQueryable<TSource> ordered
                        && query.Expression.Type == typeof(IOrderedQueryable<TSource>)
                        ? ordered.ThenByDescending(property)
                        : query.OrderByDescending(property);
                }
            }

            return query.Skip(request.SkipCount).Take(request.PageSize);
        }

        public static async Task<DatasourceResult<TSource>> ToDatasourceResultAsync<TSource>(this IQueryable<TSource> query, DatasourceRequest request, CancellationToken token)
        {
            var count = await query.CountAsync(token);

            var result = new DatasourceResult<TSource>
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Count = count,
                Data = await ApplyQuery(query, request).ToListAsync(token)
            };
            return result;
        }
    }
}
