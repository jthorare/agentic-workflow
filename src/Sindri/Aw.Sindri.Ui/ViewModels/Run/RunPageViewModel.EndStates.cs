using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's ending (<see cref="RunPageViewModel"/>, continued): the failed, didn't-work-out and cancelled states, "What went wrong" read from the instance events, and the next options (F3 spec 3 §8.5).</summary>
internal sealed partial class RunPageViewModel
{
    private string? _failureInstance;   // the instance whose events say why: the failed stage's child, else the line
    private long _failureAt;            // a Show counter: an events answer for an older Show is dropped
    private bool _failureHeard;
    private string? _again;             // the address of the run this one is another attempt of

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

    /// <summary>"Taken over by you at 11:04" while the run is with a person; empty otherwise.</summary>
    public string TakenOverText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Run again, when the run ended and lists the command; null otherwise. A run that starts opens its page.</summary>
    public ControlViewModel? Rerun { get; private set => Set(ref field, value); }

    /// <summary>"again of ITM-…/1": this run is another attempt, and the link goes to the old one; empty when it is not.</summary>
    public string AgainText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when this run is another attempt of an earlier one.</summary>
    public bool HasAgain => AgainText.Length > 0;

    /// <summary>Opens the run this one is another attempt of.</summary>
    internal void OpenAgain()
    {
        if (_again is { } old)
        {
            Go(new DeepLink(DeepLinkKind.Workflow, old));
        }
    }

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
        Attempts(line, ended: end is not null);
        if (end is not null)
        {
            Cancel = null;   // nothing left to cancel
            StartedText = Took(line, children);
        }
    }

    // Run again (an ended run that lists it) and the link back to the run this one is another attempt of.
    private void Attempts(InstanceDetail line, bool ended)
    {
        Rerun = ended && line.Actions.Contains(RerunAction, StringComparer.Ordinal) ? catalog.On(RerunAction, line.Address, ct => RerunLine(line.Id, ct)) : null;
        _again = line.Again;
        AgainText = line.Again is { Length: > 0 } old ? Labels.RunAgainOf(old) : string.Empty;
        OnChanged(nameof(HasAgain));
    }

    // Run again: the server starts the next attempt; when it answers, this page goes to the new run.
    private async Task<Result<CommandAcceptedView>> RerunLine(string id, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return SindriUiErrors.NotRunning.With("Sindri is not running, so this run can't start again.");
        }

        var started = await api.RerunInstance(url, id, cancellationToken).ConfigureAwait(true);
        if (started.IsSuccess)
        {
            Go(new DeepLink(DeepLinkKind.Workflow, started.Value.Instance.Address));
        }

        return started.Map(_ => new CommandAcceptedView("rerun", 0));
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
        line.ItemId is { } item ? [new RunNextOption(Labels.RunSeeContext, new DeepLink(DeepLinkKind.ContextItem, item))] : [];

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
