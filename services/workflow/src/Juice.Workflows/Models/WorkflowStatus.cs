namespace Juice.Workflows.Models
{
    public enum WorkflowStatus
    {
        /// <summary>
        /// Workflow is idling while waiting for prerequisites
        /// <para>The activity is not executed.</para>
        /// <para>Color: default</para>
        /// </summary>
        Idle = 0,
        /// <summary>
        /// Workflow is executing
        /// <para>Color: yellow</para>
        /// </summary>
        Executing = 1,
        /// <summary>
        /// Workflow is temporary halt while waiting for a trigger to resume
        /// <para>The activity was executed but not finished.</para>
        /// <para>Color: blue</para>
        /// </summary>
        Halted = 2,
        /// <summary>
        /// Workflow is finished
        /// <para>Color: green</para>
        /// </summary>
        Finished = 3,
        /// <summary>
        /// Workflow execution failured
        /// <para>The activity was executed and failured.</para>
        /// <para>Color: red</para>
        /// </summary>
        Faulted = 4,
        /// <summary>
        /// Workflow was aborted
        /// <para>The activity was executed but aborted.</para>
        /// <para>Color: gray</para>
        /// </summary>
        Aborted = 5
    }
}
