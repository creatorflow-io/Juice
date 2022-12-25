namespace Juice.Workflows.Models
{
    public abstract class Node : INode
    {
        public abstract LocalizedString DisplayText { get; }
        public abstract LocalizedString Category { get; }

        protected IStringLocalizer Localizer { get; }

        public Node(IStringLocalizer stringLocalizer)
        {
            Localizer = stringLocalizer;
        }

        public abstract IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node);

        public abstract Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node,
            FlowContext? flow,
            CancellationToken token);

        public abstract Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);

        public virtual Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(true);


        #region Outcomes
        protected static NodeExecutionResult Outcomes(params string[] names) => new(WorkflowStatus.Finished, names);

        /// <summary>
        /// Halt and waiting for resume signal
        /// </summary>
        /// <returns></returns>
        protected static NodeExecutionResult Halt() => NodeExecutionResult.Halted;

        /// <summary>
        /// Idle
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected static NodeExecutionResult Noop(string? message = default)
        {
            if (string.IsNullOrEmpty(message)) { return NodeExecutionResult.Empty; }

            var rs = new NodeExecutionResult(WorkflowStatus.Idle, Array.Empty<string>())
            {
                Message = message
            };
            return rs;
        }

        /// <summary>
        /// Fault
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected static NodeExecutionResult Fault(string message)
            => NodeExecutionResult.Fault(message);
        #endregion


        #region IDisposable Support

        protected virtual void Cleanup() { }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        Cleanup();
                    }
                    catch (NotImplementedException) { }
                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Node()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
