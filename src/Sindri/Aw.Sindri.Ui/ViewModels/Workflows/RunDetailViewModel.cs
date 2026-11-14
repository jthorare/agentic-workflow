using System.Text.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run with every node's status, input, output and error. Nodes keep the order they were served in, grouped by node id.</summary>
internal sealed class RunDetailViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    private int _reads;
    private Uri? _url;

    /// <summary>The run's id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

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

    /// <summary>"Started by manual".</summary>
    public string TriggerText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Revision 1".</summary>
    public string RevisionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started 4m ago" while it is live, "Ended 5m ago" after.</summary>
    public string WhenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why the run failed, as served; empty otherwise.</summary>
    public string ErrorText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the run has an error.</summary>
    public bool HasError => ErrorText.Length > 0;

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

    /// <summary>Reads one run and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown or the failure is.</returns>
    internal async Task Read(Uri url, string runId, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        _url = url;
        if (!string.Equals(Id, runId, StringComparison.Ordinal))
        {
            Id = runId;
            Groups = [];   // another run: nothing of the old one stays
            Graph = null;
            (Status, Tone, NoticeText) = (string.Empty, ChipTone.Neutral, string.Empty);
            (ChipWord, ChipGlyph, ChipToneWord) = (string.Empty, string.Empty, "neutral");
            (IsFailed, FailedText) = (false, string.Empty);
            (TriggerText, RevisionText, WhenText, ErrorText, OutputsText) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Changed();
        }

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
            (IsFailed, FailedText) = (true, Labels.WfRunFailed(run.Error));
            return;
        }

        (IsFailed, FailedText) = (false, string.Empty);
        Show(run.Value);
    }

    private void Show(WorkflowRunView run)
    {
        var (glyph, word) = Labels.WfChip(run.Status);
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
        OutputsText = WorkflowJson.Pretty(run.Outputs);
        Groups = [.. run.Nodes.GroupBy(n => n.Node, StringComparer.Ordinal).Select(g => new NodeGroupViewModel(g.Key, [.. g], ControlNode))];
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

    /// <summary>
    /// Take over or Hand back on one agent node: sent once (a press while one is on the way does nothing), the note says what Sindri answered, and a success reads the run again.
    /// A refusal shows the server's text and reads nothing.
    /// </summary>
    /// <param name="node">The node run the button belongs to.</param>
    /// <param name="takeOver">True for Take over, false for Hand back.</param>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    private async Task ControlNode(NodeRunViewModel node, bool takeOver, CancellationToken cancellationToken)
    {
        if (_url is not { } url || node.IsBusy || !(takeOver ? node.CanTakeOver : node.CanHandBack))
        {
            return;
        }

        var id = Id;
        node.IsBusy = true;
        Note(string.Empty);
        try
        {
            var (ok, note, agent) = await Send(url, id, node, takeOver, cancellationToken).ConfigureAwait(true);
            if (!string.Equals(Id, id, StringComparison.Ordinal))
            {
                return;   // another run is open now
            }

            Note(note);
            if (ok)
            {
                await Read(url, id, cancellationToken).ConfigureAwait(true);
                KeepAnswered(node, agent);
            }
        }
        finally
        {
            node.IsBusy = false;
        }
    }

    // The read can lag the call (the engine writes its marker late), so what Sindri answered wins on the rebuilt row of the same node and item.
    // A row the read shows with no agent has ended since the call: it keeps no button.
    private void KeepAnswered(NodeRunViewModel node, string? agent)
    {
        var row = Groups.FirstOrDefault(g => string.Equals(g.Node, node.Node, StringComparison.Ordinal))?.Runs.FirstOrDefault(r => r.ItemIndex == node.ItemIndex);
        if (row is { Agent: not null } && agent is not null)
        {
            row.Agent = agent;
        }
    }

    private async Task<(bool Ok, string Note, string? Agent)> Send(Uri url, string id, NodeRunViewModel node, bool takeOver, CancellationToken cancellationToken)
    {
        var sent = takeOver
            ? await api.TakeOverAgentNode(url, id, node.Node, node.ItemIndex, cancellationToken).ConfigureAwait(true)
            : await api.HandBackAgentNode(url, id, node.Node, node.ItemIndex, cancellationToken).ConfigureAwait(true);
        if (sent.IsSuccess)
        {
            return (true, takeOver ? Labels.WfTakeOverDone : Labels.WfHandBackDone, sent.Value.Agent);
        }

        return (false, takeOver ? Labels.WfTakeOverRefused(sent.Error) : Labels.WfHandBackRefused(sent.Error), null);
    }

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
        OnChanged(nameof(HasOutputs));
        OnChanged(nameof(IsError));
        OnChanged(nameof(CanCancel));
        OnChanged(nameof(CanPressCancel));
    }

    // The trigger is the stored object; its "kind" names it. One without a kind reads as an unknown trigger rather than failing the run.
    private static string KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is { Length: > 0 } text
            ? text
            : Labels.WfUnknownTrigger;
}
