using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Run;

internal static partial class RunGraphBuilder
{
    // A stage's steps are its child's states on the main path, without the end; each reads its glyph, its badge and its latest run.
    private static List<RunStep> StepsOf(InstanceDetail child)
    {
        var runs = child.StepRuns ?? [];
        return [.. child.States
            .Where(c => c is { Order: not null, BranchOf: null, Terminal: false })
            .OrderBy(c => c.Order).ThenBy(c => c.State, StringComparer.Ordinal)
            .Select(c =>
            {
                var own = runs.Where(r => string.Equals(r.Step, c.State, StringComparison.Ordinal)).ToList();
                return new RunStep(c.State, WorkflowNames.Of(c.State, displayName: null), StepState(c, own.LastOrDefault()), StepKindOf(c, own), c.Round);
            })];
    }

    private static RunNodeState StepState(StateCell cell, StepRun? run)
    {
        if (cell.Glyph != Api.Workflow.StateGlyph.Active)
        {
            return FromGlyph(cell.Glyph);
        }

        return cell.Badge?.Kind switch
        {
            EngineOwnedKind.Parked or EngineOwnedKind.Escalated => RunNodeState.Paused,
            EngineOwnedKind.TakenOver => RunNodeState.TookOver,
            _ => run?.State switch
            {
                "failed" or "refused" => RunNodeState.Failed,
                "cancelled" or "abandoned" => RunNodeState.Cancelled,
                _ => cell.Desk ? RunNodeState.Waiting : RunNodeState.Running,
            },
        };
    }

    // Who does it, as far as what is served says: a sub-workflow, an agent run, the desk; nothing else is invented.
    private static RunStepKind StepKindOf(StateCell cell, List<StepRun> runs) =>
        cell.Child ? RunStepKind.Sub
        : runs.Exists(r => string.Equals(r.Runtime, "agent", StringComparison.Ordinal)) ? RunStepKind.Agent
        : cell.Desk || runs.Exists(r => string.Equals(r.Runtime, "desk", StringComparison.Ordinal)) ? RunStepKind.You
        : RunStepKind.Unknown;
}
