using System.Globalization;
using Temporalio.Api.History.V1;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The lines a timed-out Update wait prints for a run's history (#2080): one per event, with the update id and what happened to it.</summary>
internal static class UpdateHistoryDump
{
    // why: an Update the validator rejects writes no event, so a missing id means "not delivered yet" or "rejected", never "failed"
    public const string MissingIdNote = "no line for an id = not delivered yet, or rejected by its validator (rejections aren't in history)";

    public static IEnumerable<string> Describe(IEnumerable<HistoryEvent> events)
    {
        foreach (var e in events)
        {
            var update = string.Empty;
            if (e.WorkflowExecutionUpdateAdmittedEventAttributes is { } admitted)
            {
                update = " update " + admitted.Request.Meta.UpdateId + " admitted";
            }
            else if (e.WorkflowExecutionUpdateAcceptedEventAttributes is { } accepted)
            {
                update = " update " + accepted.AcceptedRequest.Meta.UpdateId + " accepted";
            }
            else if (e.WorkflowExecutionUpdateCompletedEventAttributes is { } completed)
            {
                update = " update " + completed.Meta.UpdateId + (completed.Outcome.Failure is null ? " completed" : " failed");
            }

            yield return "  #" + e.EventId.ToString(CultureInfo.InvariantCulture) + " " + e.EventType + update;
        }
    }
}
