namespace Juice.Workflows.Bpmn.Models
{
    public static class Constants
    {
        public static Dictionary<string, string> NodeTypesMapping
            => new Dictionary<string, string> {
                { "tParallelGateway", "ParallelGateway"},
                { "tInclusiveGateway", "InclusiveGateway"},
                { "tExclusiveGateway", "ExclusiveGateway" },
                { "tEventBasedGateway", "EventBasedGateway"},
                //{ "tComplexGateway", "ComplexGateway"},
                //{ "tStartEvent", "StartEvent"},
                { "tUserTask", "UserTask"},
                { "tServiceTask", "ServiceTask"},
                { "tSendTask", "SendTask"},
                { "tReceiveTask", "ReceiveTask"},
                { "tManualTask", "ManualTask"},
                { "tBusinessRuleTask", "BusinessRuleTask"}
            };
    }
}
