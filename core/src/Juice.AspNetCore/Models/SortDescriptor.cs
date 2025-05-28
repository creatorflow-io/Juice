namespace Juice.AspNetCore.Models
{

    /// <summary>
    /// Sort data
    /// </summary>
    public class SortDescriptor
    {
        /// <summary>
        /// Sort property
        /// </summary>
        public string? Property { get; init; }
        /// <summary>
        /// Sort direction
        /// </summary>
        public SortDirection Direction { get; init; }
    }

}
