namespace Juice.Storage.Abstractions
{
    public class CreateFileOptions
    {
        public FileExistsBehavior FileExistsBehavior { get; set; }
    }

    /// <summary>
    /// Decide what happends when a file upload request already esists on storage
    /// </summary>
    public enum FileExistsBehavior
    {
        RaiseError = 0,
        Replace = 1,
        AscendedCopyNumber = 2
    }
}
