namespace Aw.Architecture.Tests.WorkflowFixtures;

/// <summary>Not a workflow type, though it sits next to them: Task.Delay and ConfigureAwait(false) are fine here (an ARCH-WF-001 fixture).</summary>
internal static class NotAWorkflow
{
    internal static async Task Wait(CancellationToken token) => await Task.Delay(1, token).ConfigureAwait(false);
}
