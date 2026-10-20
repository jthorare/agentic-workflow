using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>An instance's workflow ledger event, as the step-run fold reads it.</summary>
/// <param name="Type">The ledger event type ('workflow.started', 'workflow.transition', 'workflow.refused', …).</param>
/// <param name="At">When the ledger recorded it.</param>
/// <param name="Actor">The envelope's actor, the fallback when the payload names none.</param>
/// <param name="Payload">The payload.</param>
public sealed record RecordedWorkflowEvent(string Type, DateTimeOffset At, string Actor, WorkflowEventPayload Payload);
