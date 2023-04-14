namespace Juice.Modular
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Feature : Attribute
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string[] Dependencies { get; set; } = Array.Empty<string>();

        public string[] NonCompatibles { get; set; } = Array.Empty<string>();

        public bool Required { get; set; }
    }
}
