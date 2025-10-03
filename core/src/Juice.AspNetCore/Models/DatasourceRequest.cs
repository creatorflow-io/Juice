using System.Text.RegularExpressions;
using Juice.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Juice.AspNetCore.Models
{
    /// <summary>
    /// Basic datasource request.
    /// </summary>
    public class DatasourceRequest
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
        public SortDescriptor[] Sorts { get; set; } = new SortDescriptor[0];

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

            foreach (SortDescriptor sort in Sorts)
            {
                if (string.IsNullOrEmpty(sort.Property))
                {
                    throw new ArgumentNullException("Sort.Property");
                }
            }
        }

    }

}
