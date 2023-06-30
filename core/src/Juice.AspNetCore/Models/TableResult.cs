namespace Juice.AspNetCore.Models
{
    public class TableResult<T>
    {
        /// <summary>
        /// Current page number, min value is 1
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Current page size
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Data set of current page
        /// </summary>
        public T[] Data { get; set; } = Array.Empty<T>();

        /// <summary>
        /// Total count of data set without pagination
        /// </summary>
        public int Count { get; set; }
    }
}
