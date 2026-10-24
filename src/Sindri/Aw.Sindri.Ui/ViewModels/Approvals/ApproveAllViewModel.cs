using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>
/// The approve-all dialog (setup-once spec §4, §5.3): the plan in words, then one Approve click. The approval runs as a job the dialog
/// follows. Nothing here approves on its own: only the person's click, and one of Sindri's own steps is refused by the server (SND-AUTH-002),
/// which the dialog says in plain words.
/// </summary>
internal sealed partial class ApproveAllViewModel : ObservableObject
{
    /// <summary>The catalog action the dialog stands for.</summary>
    internal const string Action = "ApproveAllRecipes";

    private static readonly TimeSpan _pollEvery = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _pollFor = TimeSpan.FromMinutes(2);
    private const int ReadFailuresAllowed = 3;

    private readonly CatalogViewModel _catalog;
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private ApproveAllPlan? _plan;
    private int _session;   // which opening this is; an answer is for the one that asked

    /// <summary>The dialog over its ports.</summary>
    /// <param name="catalog">The command catalog, for its clipboard.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock for the job's polling.</param>
    internal ApproveAllViewModel(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(clock);
        (_catalog, _url, _api, _clock) = (catalog, url, api, clock);
    }

    /// <summary>The polite live region (a hold starting, being let go, the result).</summary>
    public LiveAnnouncer Live { get; } = new(Labels.ApproveAllTitle);

    /// <summary>The dialog's title.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.ApproveAllTitle;

    /// <summary>The line under the plan's heading.</summary>
    public string Why => Labels.ApproveWhy;

    /// <summary>The close button's word: "Close", or "Done" once the approval ended.</summary>
    public string CloseLabel => Phase == ApproveAllPhase.Done ? Labels.ApproveDone : Labels.ApproveClose;

    /// <summary>What shows while the plan is read.</summary>
    public string ReadingText => Labels.ApproveReading;

    /// <summary>The own-look heading.</summary>
    public string OwnHeading => Labels.ApproveOwnHeading;

    /// <summary>The own-look hint.</summary>
    public string OwnHint => Labels.ApproveOwnHint;

    /// <summary>The Details disclosure's heading.</summary>
    public string DetailsHeading => Labels.ApproveDetails;

    /// <summary>The Approve button's word.</summary>
    public string ApproveLabel => Labels.ApproveAllButton;

    /// <summary>The heading over what was not approved.</summary>
    public string NotApprovedHeading => Labels.ApproveNotApproved;

    /// <summary>The heading over what waits on those.</summary>
    public string WaitingHeading => Labels.ApproveWaiting;

    /// <summary>The line under the plan in the setup row: the shipped workflows pass their own checks when Sindri is built.</summary>
    public string EveryText => Labels.SetupWorkflowsEvery;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the dialog is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>Where the dialog is.</summary>
    public ApproveAllPhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            foreach (var name in _phaseDependents)
            {
                OnChanged(name);
            }
        }
    }

    private static readonly string[] _phaseDependents =
    [
        nameof(IsReading), nameof(ShowsPlan), nameof(IsEmpty), nameof(IsApplying), nameof(IsDone), nameof(IsFailed), nameof(CloseLabel), nameof(CanApprove),
    ];

    /// <summary>True while the plan is read.</summary>
    public bool IsReading => Phase == ApproveAllPhase.Reading;

    /// <summary>True while the plan shows (it is there to approve, or the person is held off).</summary>
    public bool ShowsPlan => Phase == ApproveAllPhase.Plan;

    /// <summary>True when nothing waits for approval.</summary>
    public bool IsEmpty => Phase == ApproveAllPhase.Empty;

    /// <summary>True while the approval runs.</summary>
    public bool IsApplying => Phase == ApproveAllPhase.Applying;

    /// <summary>True once the approval ended.</summary>
    public bool IsDone => Phase == ApproveAllPhase.Done;

    /// <summary>True when the plan or the approval could not be read or run.</summary>
    public bool IsFailed => Phase == ApproveAllPhase.Failed;

    /// <summary>Opens the dialog and reads the plan.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the plan is shown (or failed).</returns>
    internal async Task Open(CancellationToken cancellationToken)
    {
        if (_url() is not { } api)
        {
            return;
        }

        var session = Restart();
        IsOpen = true;
        Phase = ApproveAllPhase.Reading;
        var read = await _api.ApproveAllPlan(api, cancellationToken).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        ShowPlan(read);
    }

    /// <summary>Copies a command (a refused workflow's own approval) to the clipboard.</summary>
    /// <param name="cli">The command.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task Copy(string cli)
    {
        if (cli.Length > 0)
        {
            _ = await _catalog.Clipboard.SetText(cli, CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>Closes the dialog. An approval that already started goes on in Sindri; the dialog stops following it.</summary>
    internal void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _session++;
        ResetAnswerState();
        IsOpen = false;
        Phase = ApproveAllPhase.Closed;
        Live.Quiet();
    }

    // A new opening: nothing of the last one is left, and an answer to it is dropped.
    private int Restart()
    {
        _session++;
        (_plan, IsSending) = (null, false);
        ResetPlanState();
        ResetAnswerState();
        return _session;
    }
}
