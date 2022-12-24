namespace Juice.Workflows.Models
{
    public class SequenceFlow : IFlow
    {
        public async Task<bool> PreSelectCheckAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow)
        {
            await Task.Yield();
            if (context.IsDefaultOutgoing(flow, source))
            {
                return true;
            }
            #region ExclusiveGateway rules

            if (source.Node is IExclusive
                && !(source.Node is IEventBased)
                && context.AnyActiveFlowFrom(source))
            {
                return false;
            }

            if (dest.Node is IExclusive
                && context.AnyActiveFlowTo(dest, default))
            {
                return false;
            }
            #endregion

            #region ParallelGateway rules
            if (source.Node is ParallelGateway || dest.Node is ParallelGateway)
            {
                return true;
            }
            #endregion

            #region EventBasedGateway rules

            if (source.Node is IEventBased)
            {
                if (!(dest.Node is IIntermediate && dest.Node is ICatching))
                {
                    throw new InvalidOperationException("The nodes next to EventBasedGateway must be intermediate caching event");
                }
                return true;
            }

            #endregion

            if (flow.Record.ConditionExpression == null)
            {
                return true;
            }
            //@TODO: expresion check
            return context.GetOutcomes(source.Record.Id).Contains(flow.Record.ConditionExpression);
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).

                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~SequenceFlow()
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
