using Aw.Sindri.Ui.Api.Workflow;
using ApiGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run drawer's stage list and callout (<see cref="RunInspectorViewModel"/>, continued; F3 workflows spec §4 "Line stage statuses (inspector)").</summary>
internal sealed partial class RunInspectorViewModel
{
    // The stage names of the line workflow come from the lane table (a stopgap until the engine serves names); any other state reads its id made readable.
    // "redesign" shares the Design lane on the board but is its own stage here: after Build and prove it must not read "Design" a second time.
    internal static string StageName(string state) =>
        string.Equals(state, "redesign", StringComparison.Ordinal) ? Labels.RunStageRedesign : LineStages.LaneOf(state) is { } lane ? LineStages.Name(lane) : WorkflowNames.Of(state, displayName: null);

    private static RunStatus Of(string word) => RunStatus.From(new StateChip(word, ChipTone.Neutral));

    // The stage list: the glyph of each state gives done or not started; the state the run is in reads running, or waiting when a child waits on the user.
    private List<InspectorStageViewModel> StagesOf()
    {
        if (_detail is not { } detail || Row is null)
        {
            return [];
        }

        var stages = new List<InspectorStageViewModel>();
        foreach (var cell in detail.States.Where(s => !s.Terminal))
        {
            var current = string.Equals(cell.State, detail.State, StringComparison.Ordinal);
            var status = cell.Glyph switch
            {
                ApiGlyph.Done => Of("DONE"),
                ApiGlyph.Active => ActiveStatus(detail, cell.State),
                ApiGlyph.Pending => Of("QUEUED"),
                _ => Of("QUEUED"),
            };
            stages.Add(new InspectorStageViewModel(
                stages.Count + 1,
                StageName(cell.State),
                current ? Row.Now : string.Empty,
                current && !IsEnded ? Labels.Age(Age(detail)) : string.Empty,   // PENDING (engine): finished stages serve no time
                status,
                current));
        }

        return stages;
    }

    // The most urgent thing a child of the stage shows (waiting for the user, then paused, then taken over), else the run's own status when it is working.
    private RunStatus ActiveStatus(InstanceDetail detail, string state)
    {
        var children = detail.Children.Where(g => string.Equals(g.State, state, StringComparison.Ordinal))
            .SelectMany(g => g.Workstreams.SelectMany(w => w.Rows).Concat(g.Rows))
            .Select(r => RunStatus.From(r.Chip))
            .ToList();
        foreach (var urgent in new[] { RunStatusKind.WaitingForYou, RunStatusKind.Paused, RunStatusKind.TookOver })
        {
            if (children.Find(c => c.Kind == urgent) is { } found)
            {
                return found;
            }
        }

        return Row!.Status.Kind is RunStatusKind.WaitingForYou or RunStatusKind.Paused or RunStatusKind.TookOver ? Row.Status : Of("RUNNING");   // why: Row is set whenever a detail is
    }

    private InspectorCallout? CalloutOf()
    {
        if (Row is not { } row)
        {
            return null;
        }

        var status = row.Status;
        var next = _detail?.Current.Status.Next ?? string.Empty;
        var what = row.Now.Length > 0 ? row.Now : row.Subject;
        return status.Kind switch
        {
            RunStatusKind.WaitingForYou => new(InspectorCalloutKind.NeedsYou, status.Class, status.Icon, Labels.RunNeedsTitle(what), next.Length > 0 ? next : Labels.RunNeedsBody),
            RunStatusKind.Paused => new(InspectorCalloutKind.Paused, status.Class, status.Icon, status.Word, next.Length > 0 ? next : Labels.RunNeedsBody),
            RunStatusKind.Failed or RunStatusKind.DidntWorkOut => new(InspectorCalloutKind.Failed, status.Class, status.Icon, status.Word, Labels.RunFailedBody),
            RunStatusKind.TookOver => new(InspectorCalloutKind.TookOver, status.Class, status.Icon, Labels.RunTookTitle, next),
            RunStatusKind.Running => new(InspectorCalloutKind.Running, "neutral", status.Icon, what, next),
            RunStatusKind.Unknown or RunStatusKind.NotStarted or RunStatusKind.Done or RunStatusKind.Cancelled => null,
            _ => null,
        };
    }
}
