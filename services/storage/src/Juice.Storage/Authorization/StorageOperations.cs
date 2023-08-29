namespace Juice.Storage.Authorization
{
    public class StorageOperations
    {
        public static StorageAuthorizationRequirement Write { get; } = new(nameof(Write));
        public static StorageAuthorizationRequirement Read { get; } = new(nameof(Read));
        public static StorageAuthorizationRequirement Delete { get; } = new(nameof(Delete));
        public static StorageAuthorizationRequirement RenameFile { get; } = new(nameof(RenameFile));
    }
}
