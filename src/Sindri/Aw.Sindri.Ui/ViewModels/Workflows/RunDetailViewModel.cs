using System.Text.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run with every node's status, input, output and error. Nodes keep the order they were served in, grouped by node id.</summary>
internal sealed partial class RunDetailViewModel(ISindriApi api, TimeProvider clock, IClipboard? clipboard = null) : ObservableObject
{
    private int _reads;
    private Uri? _url;
    private string? _toMarkSeen;

    /// <summary>The run's id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Run morning-check@1:1".</summary>
    public string TitleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The served status word; empty before the first answer.</summary>
    public string Status { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip's tone; the glyph and word carry the status too.</summary>
    public ChipTone Tone { get; private set => Set(ref field, value); } = ChipTone.Neutral;

    /// <summary>The chip's word without its glyph.</summary>
    public string ChipWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip's glyph.</summary>
    public string ChipGlyph { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip's StateChipView tone: ok, running, waiting, error or neutral.</summary>
    public string ChipToneWord { get; private set => Set(ref field, value); } = "neutral";

    /// <summary>True for a failed run (the error colour).</summary>
    public bool IsError => Tone == ChipTone.Fail;

    /// <summary>True while the run is running or waiting: Cancel run shows only then.</summary>
    public bool CanCancel => RunStatus.CanCancel(Status);

    /// <summary>True while Cancel's call is on the way; a second press does nothing.</summary>
    public bool IsCancelling { get; private set { Set(ref field, value); OnChanged(nameof(CancelText)); OnChanged(nameof(CanPressCancel)); } }

    /// <summary>True when Cancel run can be pressed.</summary>
    public bool CanPressCancel => CanCancel && !IsCancelling;

    /// <summary>The Cancel button's words: "Cancel run", or "Cancelling…" while its call is on the way.</summary>
    public string CancelText => IsCancelling ? Labels.WfCancelRunBusy : Labels.WfCancelRun;

    /// <summary>What the last cancel said, in words; empty otherwise.</summary>
    public string NoticeText { get; private set { Set(ref field, value); OnChanged(nameof(HasNotice)); } } = string.Empty;

    /// <summary>True when there is a note under the run's title.</summary>
    public bool HasNotice => NoticeText.Length > 0;

    /// <summary>"Started by hand".</summary>
    public string TriggerText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Revision 1".</summary>
    public string RevisionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started 4m ago" while it is live, "Ended 5m ago" after.</summary>
    public string WhenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why the run failed, as served; empty otherwise.</summary>
    public string ErrorText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the run has an error that Sindri gave no sentence for (a run with one shows <see cref="Failure"/> and keeps the text under its Details).</summary>
    public bool HasError => ErrorText.Length > 0 && Failure is null;

    /// <summary>The failed step in words, or null.</summary>
    public RunFailureViewModel? Failure { get; private set => Set(ref field, value); }

    /// <summary>True when the failure has a sentence.</summary>
    public bool HasFailure => Failure is not null;

    /// <summary>The run's outputs as indented JSON; empty while none.</summary>
    public string OutputsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the run has outputs.</summary>
    public bool HasOutputs => OutputsText.Length > 0;

    /// <summary>The heading over the outputs.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RetryText => Labels.WfTryAgain;

    public string OutputsHeading => Labels.WfOutputsHeading;

    /// <summary>"Reading the run…" until the first answer.</summary>
    public string ReadingText => Labels.WfRunReading;
#pragma warning restore CA1822, MA0041

    /// <summary>The nodes, grouped by id, in the order first served.</summary>
    public IReadOnlyList<NodeGroupViewModel> Groups { get; private set => Set(ref field, value); } = [];

    /// <summary>True until the answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); }

    /// <summary>True when the read failed.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Opens the run page on one run: reads it, and marks a failed run seen once for this open so its Home row clears.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read and the mark.</param>
    /// <returns>A task that completes when the run is shown or the failure is.</returns>
    internal Task Open(Uri url, string runId, CancellationToken cancellationToken)
    {
        _toMarkSeen = runId;
        return Read(url, runId, cancellationToken);
    }

    /// <summary>Reads one run and shows it. A read alone never marks the run seen; only <see cref="Open"/> does.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown or the failure is.</returns>
    internal async Task Read(Uri url, string runId, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        _url = url;
        SwitchTo(runId);
        IsReading = Groups.Count == 0;
        if (IsReading)
        {
            (IsFailed, FailedText) = (false, string.Empty);   // the old failure line hides while this read is on its way
        }

        var run = await api.WorkflowRun(url, runId, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since
        }

        IsReading = false;
        if (run.IsError)
        {
            if (string.Equals(_startingId, runId, StringComparison.Ordinal))
            {
                ShowStarting(runId);   // the views row is not there yet: not a failure
                return;
            }

            (IsFailed, FailedText) = (true, Labels.WfRunFailed(run.Error));
            return;
        }

        (_startingId, _timedOutId, DidNotStart) = (null, null, false);   // the run landed, late or not: it replaces the did-not-start page
        (IsFailed, FailedText) = (false, string.Empty);
        Show(run.Value);
        await MarkSeen(url, run.Value, cancellationToken).ConfigureAwait(true);
    }

    // A read of another run empties the page first; the old open's mark is not wanted then.
    private void SwitchTo(string runId)
    {
        if (string.Equals(Id, runId, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(_toMarkSeen, runId, StringComparison.Ordinal))
        {
            _toMarkSeen = null;
        }

        Empty(runId);
    }

    // Another run: nothing of the old one stays.
    private void Empty(string runId)
    {
        Id = runId;
        Groups = [];
        Graph = null;
        (Status, Tone, NoticeText) = (string.Empty, ChipTone.Neutral, string.Empty);
        (ChipWord, ChipGlyph, ChipToneWord) = (string.Empty, string.Empty, "neutral");
        (IsFailed, FailedText) = (false, string.Empty);
        (TitleText, TriggerText, RevisionText, WhenText, ErrorText, OutputsText) = (Labels.WfRunTitle(runId), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        Failure = null;
        Changed();
    }

    // Once per open: the first read that shows the run failed sends the mark; a run that ended another way has nothing to mark.
    // The read does not say whether the run is a test run, so the mark goes anyway and Sindri answers Marked=false for one.
    private async Task MarkSeen(Uri url, WorkflowRunView run, CancellationToken cancellationToken)
    {
        if (!string.Equals(_toMarkSeen, run.Id, StringComparison.Ordinal) || RunStatus.IsLive(run.Status))
        {
            return;
        }

        _toMarkSeen = null;
        if (string.Equals(run.Status, "failed", StringComparison.Ordinal))
        {
            _ = await api.MarkWorkflowRunSeen(url, run.Id, cancellationToken).ConfigureAwait(true);   // best effort: a failed mark leaves the Home row, the page shows the run as read
        }
    }

    private void Show(WorkflowRunView run)
    {
        var (glyph, word) = Labels.WfChip(run.Status);
        TitleText = Labels.WfRunTitle(run.Id);
        (ChipWord, ChipGlyph) = (word, glyph);
        (Status, Tone) = (run.Status, RunStatus.Tone(run.Status));
        ChipToneWord = RunStatus.ToneWord(run.Status);
        TriggerText = Labels.WfStartedBy(KindOf(run.Trigger));
        RevisionText = Labels.WfRunRevision(run.Revision);
        var live = RunStatus.IsLive(run.Status);
        WhenText = live
            ? Labels.JobWhen(running: true, clock.GetUtcNow() - run.StartedAt)
            : Labels.JobWhen(running: false, clock.GetUtcNow() - (run.EndedAt ?? run.StartedAt));
        ErrorText = run.Error ?? string.Empty;
        var failedNode = FailedNode(run);
        Failure = failedNode?.Failure is { } failed ? new RunFailureViewModel(failed, run.Error ?? failed.Detail, clipboard, clock, failedNode.Node) : null;
        OutputsText = WorkflowJson.Pretty(run.Outputs);
        Groups = [.. run.Nodes.GroupBy(n => n.Node, StringComparer.Ordinal).Select(g => new NodeGroupViewModel(g.Key, [.. g], ControlNode))];
        // No next-run time on purpose (#4188): this page shows a past run, so its Schedule card carries no "next ..." chip.
        Graph = Shape is { } shape && shape.Revision == run.Revision ? WorkflowGraphViewModel.Keeping(Graph, new WorkflowGraphViewModel(shape, Statuses(run))) : null;
        Changed();
    }

    /// <summary>The run's graph with each node's status, or null when the run walked another revision than the one read (positions belong to a revision).</summary>
    public WorkflowGraphViewModel? Graph { get; private set => Set(ref field, value); }

    /// <summary>The workflow revision the page read, set before <see cref="Read"/>; null draws no graph.</summary>
    internal WorkflowView? Shape { get; set; }

    /// <summary>Marks Cancel's call as on the way, or as done.</summary>
    /// <param name="cancelling">True while the call is on the way.</param>
    internal void SetCancelling(bool cancelling) => IsCancelling = cancelling;

    /// <summary>Shows what Cancel said, in words.</summary>
    /// <param name="text">The note; empty clears it.</param>
    internal void Note(string text) => NoticeText = text;

    // a node run per item of a for_each: the last one served speaks for the node
    private static Dictionary<string, string> Statuses(WorkflowRunView run)
    {
        var statuses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in run.Nodes)
        {
            statuses[node.Node] = node.Status;
        }

        return statuses;
    }

    private void Changed()
    {
        OnChanged(nameof(HasError));
        OnChanged(nameof(HasFailure));
        OnChanged(nameof(HasOutputs));
        OnChanged(nameof(IsError));
        OnChanged(nameof(CanCancel));
        OnChanged(nameof(CanPressCancel));
    }

    // The trigger is the stored object; its "kind" names it. One without a kind reads as "an unknown trigger" rather than failing the run.
    private static string? KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is { Length: > 0 } text
            ? text
            : null;

    // The callout is for a run that failed: a step that failed and went on (onFailure 'continue' or 'skip-item') leaves a failed row in a run that succeeded or is still going.
    // When several rows failed, the one the run's own error names ("node 'check' failed: ...") is the step the run stopped at, else the last.
    private static NodeRunView? FailedNode(WorkflowRunView run)
    {
        if (string.IsNullOrEmpty(run.Error))
        {
            return null;
        }

        var failed = run.Nodes.Where(static n => n.Failure is not null).ToList();
        return failed.LastOrDefault(n => run.Error.Contains($"node '{n.Node}'", StringComparison.Ordinal)) ?? failed.LastOrDefault();
    }
}
