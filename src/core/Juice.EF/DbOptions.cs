namespace Juice.EF
{
    public abstract class DbOptions
    {
        public string DatabaseProvider { get; set; }
        public string ConnectionName { get; set; }
    }
}
