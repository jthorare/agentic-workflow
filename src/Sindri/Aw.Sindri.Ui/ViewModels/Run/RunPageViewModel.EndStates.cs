using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's ending (<see cref="RunPageViewModel"/>, continued): the failed, didn't-work-out and cancelled states, "What went wrong" read from the instance events, and the next options (F3 spec 3 §8.5).</summary>
internal sealed partial class RunPageViewModel
{
    private string? _failureInstance;   // the instance whose events say why: the failed stage's child, else the line
    private long _failureAt;            // a Show counter: an events answer for an older Show is dropped
    private bool _failureHeard;

    /// <summary>True while a failed step is shown with its reason (or the line that none was recorded).</summary>
    public bool HasFailure { get; private set => Set(ref field, value); }

    /// <summary>The failed step's name, or the stage's when no step failed.</summary>
    public string FailureStep { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why it failed, in words.</summary>
    public string FailureText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The block's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string FailureTitle => Labels.RunWhatWentWrong;
#pragma warning restore CA1822, MA0041

    /// <summary>What the person can do next: only what the server serves, and the run's context.</summary>
    public IReadOnlyList<RunNextOption> NextOptions { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the run has ended (a reached end card exists).</summary>
    public bool HasEnded { get; private set => Set(ref field, value); }

    // Called by Show once the cards are known: the header, Cancel, the failure block's frame and the next options.
    private void EndStates(InstanceDetail line, RunGraph graph, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        var end = graph.Nodes.FirstOrDefault(n => n.Kind == RunNodeKind.End && IsReached(line, n));
        HasEnded = end is not null;
        var bad = RunEndStates.BadStage(graph);
        var failed = HasFailed(end, bad);
        _failureAt++;
        _failureHeard = false;
        _failureInstance = failed ? bad?.ChildId ?? line.Id : null;
        HasFailure = failed;
        FailureStep = FailedStepName(bad);
        FailureText = failed ? Labels.RunNoDetail : string.Empty;
        NextOptions = end is null ? [] : OptionsOf(line);
        if (end is not null)
        {
            Cancel = null;   // nothing left to cancel
            StartedText = Took(line, children);
        }
    }

    private static bool HasFailed(RunNode? end, RunNode? bad) =>
        bad?.State == RunNodeState.Failed || end?.State == RunNodeState.Failed || (end?.State == RunNodeState.DidntWorkOut && bad is not null);

    private static string FailedStepName(RunNode? bad) =>
        bad is null ? string.Empty : bad.Steps.Where(s => s.State == RunNodeState.Failed).Select(s => s.Name).FirstOrDefault() ?? bad.Name;

    private string Took(InstanceDetail line, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        var began = children.Values.SelectMany(c => c.StepRuns ?? []).Min(r => (DateTimeOffset?)r.StartedAt);
        return began is { } from
            ? Labels.RunTook(Labels.Duration((long)Math.Max(0, (line.Since - from).TotalSeconds)))
            : Labels.RunEndedAgo(Labels.Duration((long)Math.Max(0, (clock.GetUtcNow() - line.Since).TotalSeconds)));
    }

    // Only what is served: the run's context, and no action this page does not know how to draw (takeover and a re-run wait on the engine, spec 3 §9).
    private static IReadOnlyList<RunNextOption> OptionsOf(InstanceDetail line) =>
        line.ItemId is { } item ? [new RunNextOption(Labels.RunSeeContext, new DeepLink(DeepLinkKind.Item, item))] : [];

    // Reads the events of the failed instance and puts the reason of its last failed step in the block.
    private async Task ReadWhatWentWrong(Uri url, long at, CancellationToken cancellationToken)
    {
        if (_failureHeard || !HasFailure || _failureInstance is not { } instance)
        {
            return;
        }

        var events = await api.InstanceEvents(url, instance, cancellationToken).ConfigureAwait(true);
        if (at != _failureAt || events.IsError)
        {
            return;
        }

        var last = events.Value.Events.LastOrDefault(e => string.Equals(e.Transition?.Trigger, "step.failed", StringComparison.Ordinal));
        if (last?.Transition?.Reason is { Length: > 0 } reason)
        {
            FailureText = Labels.RunReason(reason);
        }

        _failureHeard = true;
    }
}
