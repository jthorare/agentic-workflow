using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The run drawer (F3 workflows spec §2 "Inspector drawer", §4, §5): one run's header, its callout, its stages and its actions. The header and the
/// callout come from the row the page already holds; the stages come from the instance read. It sends nothing: Open run, Take over, Hand back and
/// Cancel run only ask the shell to open the run (the frame holds the commands), and nothing is approved here.
/// </summary>
internal sealed partial class RunInspectorViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    private Uri? _url;
    private InstanceDetail? _detail;
    private int _reads;

    /// <summary>Raised when an action asks to go somewhere: the run, the run with an intent, or the place that decides it.</summary>
    internal event EventHandler<OpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when the close button was pressed.</summary>
    internal event EventHandler? CloseRequested;

    /// <summary>Raised when Previous (-1) or Next (1) was pressed; the page moves its selection and the drawer follows.</summary>
    internal event EventHandler<StepRequestedEventArgs>? StepRequested;

    /// <summary>The run shown; null before the first.</summary>
    public InstanceRowViewModel? Row { get; private set => Set(ref field, value); }

    /// <summary>The run's address, which is also its reference; empty before the first run.</summary>
    public string Address => Row?.Address ?? string.Empty;

    /// <summary>The run's title.</summary>
    public string Title => Row?.Subject ?? string.Empty;

    /// <summary>The run's workflow, as the pill reads it.</summary>
    public string WorkflowName => Row?.DisplayName ?? string.Empty;

    /// <summary>The run's one status.</summary>
    public RunStatus? Status => Row?.Status;

    /// <summary>"Design for 1h 30m" while the run is open, "Ended 5h 20m ago" once it ended; the row's own "in stage" words until the read arrives.</summary>
    public string Meta => (Row, _detail) switch
    {
        (null, _) => string.Empty,
        ({ } row, null) => row.TimeText,
        (_, { } detail) => IsEnded
            ? Labels.RunEnded(Labels.Age(Age(detail)))
            : Labels.RunStageFor(StageName(detail.State), Labels.Age(Age(detail))),
    };

    /// <summary>The reference line: the address, for pasting into a terminal.</summary>
    public string Reference => Address;

    /// <summary>The footer's sentence.</summary>
    public string ReferenceNote => Labels.RunReferenceNote(Address);

    /// <summary>The stages of the run, in order, each with its status.</summary>
    public IReadOnlyList<InspectorStageViewModel> Stages { get; private set => Set(ref field, value); } = [];

    /// <summary>"3 of 5 done".</summary>
    public string StagesDoneText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The callout under the header; null when the run has none.</summary>
    public InspectorCallout? Callout { get; private set => Set(ref field, value); }

    /// <summary>True when there is a callout.</summary>
    public bool HasCallout => Callout is not null;

    /// <summary>True when the callout offers "Review and decide": the run waits on the user.</summary>
    public bool HasReview => Callout?.Kind is InspectorCalloutKind.NeedsYou or InspectorCalloutKind.Paused;

    /// <summary>True when "Or run in a terminal" can copy a command: the run waits on a protected answer.</summary>
    public bool HasTerminal => HasReview && Row?.Protected is { CanCopy: true };

    /// <summary>True when the callout is the took-over one, which carries Hand back.</summary>
    public bool IsTookOver => Callout?.Kind == InspectorCalloutKind.TookOver;

    /// <summary>True while Take over shows: the run is running, waiting or paused.</summary>
    public bool CanTakeOver => Row?.Status.Kind is RunStatusKind.Running or RunStatusKind.WaitingForYou or RunStatusKind.Paused;

    /// <summary>True while Hand back shows: only a run the user took over.</summary>
    public bool CanHandBack => Row?.Status.Kind == RunStatusKind.TookOver;

    /// <summary>True while Cancel run shows: the engine lists it and the run has not ended.</summary>
    public bool CanCancel => Row is { CanCancel: true } && !IsEnded;

    /// <summary>"3 of 12"; empty when the run is not among those the page walks.</summary>
    public string PositionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a run comes before this one.</summary>
    public bool CanPrevious { get; private set => Set(ref field, value); }

    /// <summary>True when a run comes after this one.</summary>
    public bool CanNext { get; private set => Set(ref field, value); }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The drawer's group name.</summary>
    public string DetailsName => Labels.RunDetails;

    /// <summary>The close button's words.</summary>
    public string CloseText => Labels.RunClose;

    /// <summary>The previous button's words.</summary>
    public string PreviousText => Labels.RunPrevious;

    /// <summary>The next button's words.</summary>
    public string NextText => Labels.RunNextRun;

    /// <summary>The copy-reference button's words.</summary>
    public string CopyReferenceText => Labels.RunCopyReference;

    /// <summary>Open run's words.</summary>
    public string OpenText => Labels.RunOpen;

    /// <summary>Take over's words.</summary>
    public string TakeOverText => Labels.RunTakeOver;

    /// <summary>Hand back's words.</summary>
    public string HandBackText => Labels.RunHandBack;

    /// <summary>Cancel run's words.</summary>
    public string CancelText => Labels.RunCancel;

    /// <summary>The stage list's caption.</summary>
    public string StagesEyebrow => Labels.RunStagesEyebrow;

    /// <summary>The needs-you callout's primary button.</summary>
    public string ReviewDecideText => Labels.RunReviewDecide;

    /// <summary>The terminal chip's words.</summary>
    public string TerminalText => Labels.RunTerminal;
#pragma warning restore CA1822, MA0041

    private bool IsEnded => Row?.Status.Kind is RunStatusKind.Done or RunStatusKind.Failed or RunStatusKind.DidntWorkOut or RunStatusKind.Cancelled;

    /// <summary>Shows a run, then reads its detail for the stages.</summary>
    /// <param name="url">The API's base URL; null reads nothing (the stages stay empty).</param>
    /// <param name="row">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the stages are shown.</returns>
    internal async Task Show(Uri? url, InstanceRowViewModel row, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        _url = url;
        if (!string.Equals(Row?.Address, row.Address, StringComparison.Ordinal))
        {
            _detail = null;   // the last run's stages are not this one's
        }

        Row = row;
        Rebuild();
        if (url is null)
        {
            return;
        }

        var result = await api.Instance(url, row.InstanceId, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer run was asked for since: its answer is the one to show
        }

        _detail = result.IsSuccess ? result.Value : null;
        Rebuild();
    }

    /// <summary>Sets where the run stands among those the page walks.</summary>
    /// <param name="n">The place, from 1; 0 when the run is not among them.</param>
    /// <param name="of">How many runs.</param>
    internal void SetPosition(int n, int of)
    {
        PositionText = n > 0 ? Labels.RunPosition(n, of) : string.Empty;
        CanPrevious = n > 1;
        CanNext = n > 0 && n < of;
    }

    /// <summary>Asks to close the drawer.</summary>
    internal void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks for the previous run.</summary>
    internal void Previous() => StepRequested?.Invoke(this, new StepRequestedEventArgs(-1));

    /// <summary>Asks for the next run.</summary>
    internal void Next() => StepRequested?.Invoke(this, new StepRequestedEventArgs(1));

    /// <summary>Open run: the run's frame.</summary>
    internal void OpenRun() => Ask(FrameIntent.None);

    /// <summary>Take over: the run's frame, where the take-over command is. Nothing is sent.</summary>
    internal void TakeOver() => Ask(FrameIntent.TakeOver);

    /// <summary>Hand back: the run's frame, where the hand-back command is. Nothing is sent.</summary>
    internal void HandBack() => Ask(FrameIntent.HandBack);

    /// <summary>Cancel run: the run's frame, which asks first.</summary>
    internal void Cancel() => Ask(FrameIntent.AskCancel);

    private void Ask(FrameIntent intent)
    {
        if (Row is { } row)
        {
            OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link, intent));
        }
    }

    /// <summary>Puts the run's reference on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the clipboard was asked.</returns>
    internal Task CopyReference(CancellationToken cancellationToken) => clipboard.SetText(Address, cancellationToken);

    /// <summary>Puts the protected answer's terminal command on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the clipboard was asked.</returns>
    internal Task CopyTerminal(CancellationToken cancellationToken) =>
        HasTerminal ? clipboard.SetText(Row!.Protected!.Cli, cancellationToken) : Task.CompletedTask;   // why: HasTerminal holds only for a row with a protected answer

    /// <summary>Review and decide: goes to the run's parked child (its frame opens at the answer), else the Needs you row of the run's item, else the item, else the run. Never nothing, and nothing is approved here.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the shell was asked.</returns>
    internal async Task ReviewAndDecide(CancellationToken cancellationToken)
    {
        if (Row is not { } row)
        {
            return;
        }

        var child = WaitingChild();
        if (_url is { } desk && (await api.NeedsYou(desk, cancellationToken).ConfigureAwait(true)) is { IsSuccess: true, Value: var parks }
            && parks.Rows.FirstOrDefault(r => IsParkOf(r, row, child)) is { } park)
        {
            OpenRequested?.Invoke(this, new OpenRequestedEventArgs(new DeepLink(DeepLinkKind.NeedsYou, park.Id), atDesk: true));   // the answer form is on the desk card, mirrored child parks included
            return;
        }

        if (child is not null)
        {
            OpenRequested?.Invoke(this, new OpenRequestedEventArgs(new DeepLink(DeepLinkKind.Workflow, child.Address), FrameIntent.OpenInitial));
            return;
        }

        var (link, intent) = await ItemOrRun(row, cancellationToken).ConfigureAwait(true);
        OpenRequested?.Invoke(this, new OpenRequestedEventArgs(link, intent));
    }

    // The Needs you row of the run's item, else the item, else the run's own frame at its children.
    private async Task<(DeepLink Link, FrameIntent Intent)> ItemOrRun(InstanceRowViewModel row, CancellationToken cancellationToken)
    {
        if (_detail?.ItemId is not { Length: > 0 } item)
        {
            return (row.Link, FrameIntent.FocusChildren);
        }

        if (_url is { } url && (await api.NeedsYou(url, cancellationToken).ConfigureAwait(true)) is { IsSuccess: true, Value: var list }
            && list.Rows.FirstOrDefault(r => string.Equals(r.Item, item, StringComparison.Ordinal)) is { } waiting)
        {
            return (new DeepLink(DeepLinkKind.NeedsYou, waiting.Id), FrameIntent.None);
        }

        return (new DeepLink(DeepLinkKind.Item, item), FrameIntent.None);
    }

    // The park row that stands for this run's wait: its own, or its waiting child's (engine #850 mirrors a child's park at the root).
    private static bool IsParkOf(NeedsYouRowView r, InstanceRowViewModel row, ChildRow? child) =>
        r.Id.StartsWith("park:", StringComparison.Ordinal)
        && (string.Equals(r.Subject?.Id, row.InstanceId, StringComparison.Ordinal)
            || (child is not null && (string.Equals(r.Subject?.Id, child.Id, StringComparison.Ordinal) || string.Equals(r.Child, child.Address, StringComparison.Ordinal))));

    // The child that waits on the user (a parked step first), as the stage list shows it.
    private ChildRow? WaitingChild()
    {
        var rows = (_detail?.Children ?? []).SelectMany(g => g.Workstreams.SelectMany(w => w.Rows).Concat(g.Rows)).ToList();
        return rows.Find(r => RunStatus.From(r.Chip).Kind == RunStatusKind.Paused) ?? rows.Find(r => RunStatus.From(r.Chip).Kind == RunStatusKind.WaitingForYou);
    }

    private TimeSpan Age(InstanceDetail detail) => clock.GetUtcNow() - detail.Since;

    private void Rebuild()
    {
        Stages = StagesOf();
        StagesDoneText = Labels.RunStagesDone(Stages.Count(s => s.Status.Kind == RunStatusKind.Done), Stages.Count);
        Callout = CalloutOf();
        foreach (var name in new[] { nameof(Address), nameof(Title), nameof(WorkflowName), nameof(Status), nameof(Meta), nameof(Reference), nameof(ReferenceNote), nameof(HasCallout), nameof(HasReview), nameof(HasTerminal), nameof(IsTookOver), nameof(CanTakeOver), nameof(CanHandBack), nameof(CanCancel) })
        {
            OnChanged(name);
        }
    }
}
