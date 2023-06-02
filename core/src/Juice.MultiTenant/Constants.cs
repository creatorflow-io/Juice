namespace Juice.MultiTenant
{
    public static class Constants
    {
        public static int TenantIdMaxLength = 64;
        public static int TenantIdentifierMaxLength = 16;
        public static int ConfigurationKeyMaxLength = 250;
        public static string TenantToken = "__tenant__";
        public static readonly string MultiTenantAnnotationName = "Finbuckle:MultiTenant";
    }
}
