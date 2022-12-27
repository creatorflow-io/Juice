namespace Juice.Workflows.Yaml.Models
{
    internal class Step
    {
        public string? Name { get; set; }
        public string Type { get; set; }
        public string? Condition { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
        public Process[]? Branches { get; set; }
        public Process? Process { get; set; }
        public string[]? MergeBranches { get; set; }
        public Step[]? BoundaryEvents { get; set; }
    }
}
