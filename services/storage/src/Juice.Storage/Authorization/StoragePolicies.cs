namespace Juice.Storage.Authorization
{
    public class StoragePolicies
    {
        public const string StoragePolicyPrefix = "Storage";
        public const string CreateFile = StoragePolicyPrefix + "_" + nameof(CreateFile);
        public const string DownloadFile = StoragePolicyPrefix + "_" + nameof(DownloadFile);
    }
}
