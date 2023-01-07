namespace Juice.Workflows.Execution
{
    public record ProcessRecord
    {
        public ProcessRecord() { }
        public ProcessRecord(string id, string? name = "default")
        {
            Id = id;
            Name = name;
        }
        public string Id { get; init; }
        public string? Name { get; init; }
    }
}
