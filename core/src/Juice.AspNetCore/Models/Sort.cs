namespace Juice.AspNetCore.Models
{

    /// <summary>
    /// Sort data
    /// </summary>
    public class Sort
    {
        /// <summary>
        /// Sort property
        /// </summary>
        public string Property { get; set; }
        /// <summary>
        /// Sort direction
        /// </summary>
        public SortDirection Direction { get; set; }
    }

}
