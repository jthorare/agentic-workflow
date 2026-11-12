using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'GraphRecordNodeAgent' writes (#3151): one 'workflow.node.agent.changed' event and its place in the node run's series, which makes the idempotency key.</summary>
/// <param name="Payload">The event's payload.</param>
/// <param name="Sequence">The event's position in the node run's series, counted from 1.</param>
internal sealed record GraphNodeAgent(WorkflowNodeAgentChangedPayload Payload, int Sequence);
