using System.Text.RegularExpressions;
using Juice.CompnentModel;
using Juice.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Juice.AspNetCore.Models
{
    /// <summary>
    /// Basic table query model.
    /// </summary>
    public class TableQuery
    {
        private string? _q;

        /// <summary>
        /// Query string.
        /// </summary>
        [FromQuery(Name = "q")]
        public string? Query
        {
            get { return _q; }
            set
            {
                _q = value;
                if (!string.IsNullOrEmpty(_q))
                {
                    _filterText = Regex.Replace($"%{_q.Trim()}%", "[\\s]+", "%");
                }
                else
                {
                    _filterText = string.Empty;
                }
            }
        }

        /// <summary>
        /// Sorts data
        /// </summary>
        public Sort[] Sorts { get; set; } = new Sort[0];

        /// <summary>
        /// Page number, start from 1.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size, default is 10.
        /// </summary>
        public int PageSize { get; set; } = 10;

        private string _filterText = string.Empty;

        [ApiIgnore]
        [BindNever]
        public string? FilterText => _filterText;

        [ApiIgnore]
        [BindNever]
        public int SkipCount => (Page - 1) * PageSize;

        public void Standardizing()
        {
            if (Page < 1)
            {
                Page = 1;
            }
            if (PageSize > 50)
            {
                PageSize = 50;
            }
            if (PageSize < 10)
            {
                PageSize = 10;
            }

            foreach (Sort sort in Sorts)
            {
                if (string.IsNullOrEmpty(sort.Property))
                {
                    throw new ArgumentNullException("Sort.Property");
                }
            }
        }

        public IQueryable<TSource> ApplyQuery<TSource>(
            IQueryable<TSource> query)
        {
            foreach (var sort in Sorts)
            {
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

            return query.Skip(SkipCount).Take(PageSize);
        }

        public async Task<TableResult<TSource>> ToTableResultAsync<TSource>(IQueryable<TSource> query, CancellationToken token)
        {
            var count = await query.CountAsync(token);

            var result = new TableResult<TSource>
            {
                Page = Page,
                PageSize = PageSize,
                Count = count,
                Data = await ApplyQuery(query).ToArrayAsync(token)
            };
            return result;
        }
    }

}
