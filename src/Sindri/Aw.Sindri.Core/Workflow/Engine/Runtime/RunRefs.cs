namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The git names of an agent run (sandbox spec SB5, SB16(4)): the only ref a sandboxed run may write is its own, so two instances on one item never collide.</summary>
public static class RunRefs
{
    /// <summary>The ref directory of a run: 'sindri/{instance}/{run}'. The sandbox leaves exactly this directory writable.</summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="run">The run's token.</param>
    /// <returns>The directory, without a trailing slash.</returns>
    public static string Namespace(WorkflowInstanceId instance, RunToken run) =>
        throw new NotSupportedException("the run ref is not built yet");

    /// <summary>The run's branch: 'sindri/{instance}/{run}/work'. A killed run's branch is kept, and a limit failure names it.</summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="run">The run's token.</param>
    /// <returns>The branch name, without 'refs/heads/'.</returns>
    public static string Branch(WorkflowInstanceId instance, RunToken run) =>
        throw new NotSupportedException("the run ref is not built yet");
}
