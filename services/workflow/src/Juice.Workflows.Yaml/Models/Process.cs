namespace Juice.Workflows.Yaml.Models
{
    internal class Process
    {
        public string? Name { get; set; }
        public Step[] Steps { get; set; } = Array.Empty<Step>();

    }
}
