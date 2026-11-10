namespace Aw.Architecture.Tests.WorkflowFixtures;

/// <summary>The part of PlantedWorkflow with no [Workflow] attribute: the rule still checks it, as the real GraphWorkflow.*.cs parts are (an ARCH-WF-001 fixture).</summary>
internal static partial class PlantedWorkflow
{
    internal static Task InPartialPart(CancellationToken token) => Task.Delay(1, token);
}
