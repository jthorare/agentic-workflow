using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Maps a workflow ledger row to the plain event the step-run fold reads, so the fold (in Core) depends on no payload type.</summary>
internal static class StepRunEventMap
{
    /// <summary>The fold's event for a row of <paramref name="type"/> recorded at <paramref name="at"/> by <paramref name="actor"/>.</summary>
    /// <param name="type">The ledger event type.</param>
    /// <param name="at">When it was recorded.</param>
    /// <param name="actor">The envelope's actor.</param>
    /// <param name="payload">The row's workflow payload.</param>
    /// <returns>The event.</returns>
    public static RecordedWorkflowEvent Of(string type, DateTimeOffset at, string actor, WorkflowEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var frame = payload.Detail?.Delta.Frame;
        return new RecordedWorkflowEvent(type, at, actor, payload.Trigger, payload.Outcome, payload.Run, payload.Tokens ?? payload.Detail?.Tokens, payload.Detail?.TriggerActor,
            frame is null ? null : new RecordedFrame(frame.State, frame.Run, frame.Runtime))
        {
            ImageCount = payload.Detail?.ImageCount,
            ImagesDropped = payload.Detail?.ImagesDropped,
        };
    }
}
