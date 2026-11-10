namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a batch of a 'for_each' tells its run with the 'agent-child' signal (#2470): the agent it waits on, or null when it waits on none.</summary>
/// <param name="View">The wait, its <see cref="AgentNodeView.Child"/> naming the batch workflow; null when the batch no longer waits.</param>
internal sealed record AgentChildSignal(AgentNodeView? View);
