namespace Juice.MultiTenant
{
    public static class Constants
    {
        public const int TenantIdMaxLength = 64;
        public const int TenantIdentifierMaxLength = 16;
        public const int TenantOwnerMaxLength = 256;
        public const int ConfigurationKeyMaxLength = 250;
        public const int ConfigurationValueMaxLength = 500;
        public const string TenantToken = "__tenant__";
        public const string MultiTenantAnnotationName = "Finbuckle:MultiTenant";
    }
}
