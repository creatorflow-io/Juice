using Juice.Domain;
using Newtonsoft.Json;

namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public class WorkflowDefinition : AuditAggregrateRoot<string, INotification>
    {

        public WorkflowDefinition() { }

        public WorkflowDefinition(string id, string name)
        {
            Id = id;
            Name = name;
        }
        /// <summary>
        /// Raw data of workflow definition in yml, json or xml format
        /// </summary>
        public string? RawData { get; private set; }
        /// <summary>
        /// Describe raw data format to deserialize to executable data
        /// </summary>
        public string? RawFormat { get; private set; }
        /// <summary>
        /// Parsed data that ready to execute
        /// </summary>
        public string? Data { get; private set; }

        public void UpdateRawData(string rawData, string rawFormat)
        {
            RawData = rawData;
            RawFormat = rawFormat;
        }

        public void ClearData()
        {
            Data = default;
            this.AddDomainEvent(new DefinitionDataChangedDomainEvent(this, Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>()));
        }

        public void SetData(IEnumerable<ProcessRecord> processes, IEnumerable<NodeData> nodes, IEnumerable<FlowData> flows)
        {
            Data = JsonConvert.SerializeObject((processes, nodes, flows));
            this.AddDomainEvent(new DefinitionDataChangedDomainEvent(this, processes, nodes, flows));
        }

        public (IEnumerable<ProcessRecord> Processes, IEnumerable<NodeData> Nodes, IEnumerable<FlowData> Flows) GetData()
        {
            return JsonConvert.DeserializeObject<(IEnumerable<ProcessRecord>, IEnumerable<NodeData>, IEnumerable<FlowData>)>(Data ?? "{}");
        }
    }
}
