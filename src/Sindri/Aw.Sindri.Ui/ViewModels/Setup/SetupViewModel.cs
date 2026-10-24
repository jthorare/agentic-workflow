using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The setup checklist, the first section of Settings, and Home's one line while any step is open (setup spec §2.4): the six steps in the app's own words, the progress strip, and
/// which rows are open. The list is read on connect and with every event, and the steps are kept by id across reads. It decides nothing: states, sentences and actions are the
/// API's; what the app adds is words, order, and the one button the map shows when it failed.
/// </summary>
internal sealed partial class SetupViewModel : ObservableObject
{
    private const string MapStep = "map";
    private const string McpStep = "mcp";
    private const string OnyxStep = "onyx";
    private const string ProfileStep = "profile";
    private const string RecipesStep = "recipes";
    private const string ProfileAction = "ApproveProfile";
    private const string MapRefresh = "RefreshContextMap";
    private const string RegisterAction = "RegisterMcp";
    private const string ConflictRefusal = "SND-JOB-409";
    private const string RunningJob = "runningJob";

    private readonly Dictionary<string, SetupStepViewModel> _rows = new(StringComparer.Ordinal);
    private readonly ISindriApi _api;
    private readonly CatalogViewModel _catalog;
    private readonly TimeProvider _clock;
    private readonly Func<string, bool> _opensInApp;
    private readonly IFolderPicker _folders;
    private readonly Func<string?> _profileHash;
    private bool _registering;
    private int _reads;
    private bool _hasRead;
    private bool _refreshing;
    private Uri? _url;
    private int _holdUntil;   // reads numbered up to this one began before the send of a retry: they must not bring the button back
    private string? _revealStep;

    /// <summary>The list over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="catalog">The command catalog: its rows give the terminal forms of the actions.</param>
    /// <param name="clock">The clock for the Copy controls' timings.</param>
    /// <param name="opensInApp">Whether the app can do an action itself (a button) rather than show the command; the shell decides.</param>
    /// <param name="folders">The folder chooser of the repo step; a head with none shows the typed path only.</param>
    /// <param name="profileHash">The live profile's hash the typed approval is tied to, or null when not known; none means never known.</param>
    internal SetupViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock, Func<string, bool> opensInApp, IFolderPicker folders, Func<string?>? profileHash = null)
    {
        (_api, _catalog, _clock, _opensInApp, _folders) = (api, catalog, clock, opensInApp, folders);
        _profileHash = profileHash ?? (static () => null);
        Launcher = new ControlViewModel(new CommandRowView("StartSindri", "desk", [], Terminal: false, Method: null, Route: null, Labels.SetupLauncher, "cli-only:launcher", Destructive: false, ReadsViews: false, Shortcut: null), send: null, catalog.Clipboard, clock);
        Footnote = new ControlViewModel(new CommandRowView("SetupOnyx", "desk", [], Terminal: false, Method: null, Route: null, Labels.SetupOnyxFootCommand, "cli-only:footnote", Destructive: false, ReadsViews: false, Shortcut: null), send: null, catalog.Clipboard, clock);
    }

    /// <summary>Raised when a step's button asks the shell to open an action in the app.</summary>
    internal event EventHandler<SetupActionEventArgs>? ActionRequested;

    /// <summary>Raised when Home's line asks for the checklist.</summary>
    internal event EventHandler? ChecklistRequested;

    /// <summary>Raised when the checklist should come into view (Settings was opened by a link, G U, ⌘, or the line).</summary>
    internal event EventHandler<SetupRevealEventArgs>? RevealRequested;

    /// <summary>The steps in the order served.</summary>
    public ObservableCollection<SetupStepViewModel> Steps { get; } = [];

    /// <summary>The polite live region: a step changing is said once.</summary>
    public LiveAnnouncer Live { get; } = new(Labels.Settings);

    /// <summary>The launcher command with Copy, for when Sindri isn't running.</summary>
    public ControlViewModel Launcher { get; }

    /// <summary>The terminal alternative for Onyx, one line under the checklist (Q5), with Copy.</summary>
    public ControlViewModel Footnote { get; }

    /// <summary>True while the footnote shows: Onyx is not set up and not set aside.</summary>
    public bool IsFootnoteShown => _rows.TryGetValue(OnyxStep, out var onyx) && !onyx.IsFinished;

    /// <summary>The words before the footnote's command.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    public string FootnoteText => Labels.SetupOnyxFoot;
#pragma warning restore CA1822, MA0041

    /// <summary>The lead over the checklist: "Set up Sindri", or "Sindri is set up"; empty until the first list.</summary>
    public string Lead { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"2 of 6 done", or "All 6 done"; empty until the first list.</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the one-line sub shows (hidden once setup is finished).</summary>
    public bool HasSub { get; private set => Set(ref field, value); }

    /// <summary>One segment per step: done, working or still to do.</summary>
    public IReadOnlyList<SetupSegment> Segments { get; private set => Set(ref field, value); } = [];

    /// <summary>The steps done, for Home's meter.</summary>
    public double MeterDone { get; private set => Set(ref field, value); }

    /// <summary>All the steps, the meter's full length.</summary>
    public double MeterMax { get; private set => Set(ref field, value); }

    /// <summary>True when every step is done or skipped.</summary>
    public bool IsFinished { get; private set => Set(ref field, value); }

    /// <summary>True when Home shows its one line: there are steps, and one is still open. Home then hides the setup rows of Needs you, which the line stands for.</summary>
    public bool IsHomeLineShown { get; private set => Set(ref field, value); }

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when this Sindri has no setup list (an older one).</summary>
    public bool IsNotServed { get; private set => Set(ref field, value); }

    /// <summary>True when the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the steps shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the first read failed (Sindri is running) and there are no steps to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed first read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when nothing answered and there are no steps to show.</summary>
    public bool IsNotRunning { get; private set => Set(ref field, value); }

    /// <summary>True once the list has steps.</summary>
    public bool HasSteps { get; private set => Set(ref field, value); }

    /// <summary>Asks the shell for Settings at the checklist (Home's line).</summary>
    internal void RequestChecklist() => ChecklistRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the checklist to come into view.</summary>
    internal void RevealChecklist()
    {
        var step = _revealStep;
        _revealStep = null;
        RevealRequested?.Invoke(this, new SetupRevealEventArgs(step));
    }

    /// <summary>The first step still to do (or being worked on), as Home's line names it; empty when setup is finished or before the first list.</summary>
    public string HomeStepText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Continue control's words; empty with no step to continue.</summary>
    public string ContinueText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Opens the first step still to do and asks for Settings at it (Home's Continue).</summary>
    internal void Continue()
    {
        if (Steps.FirstOrDefault(s => !s.IsFinished) is { } next)
        {
            next.IsOpen = true;
            _revealStep = next.Id;
        }

        ChecklistRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Registers Sindri in the approved repos again (Connect on a failed agents step). One press sends once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RegisterMcp(CancellationToken cancellationToken)
    {
        if (_registering || _url is not { } url || !_rows.TryGetValue(McpStep, out var mcp) || mcp.IsWorking)   // one press sends once
        {
            return;
        }

        _registering = true;
        try
        {
            var started = await _api.RegisterMcp(url, new RegisterMcpRequest(), cancellationToken).ConfigureAwait(true);
            if (started.IsSuccess)
            {
                Hold(mcp, Labels.SetupMcpAgain);
            }
            else
            {
                mcp.Say(Labels.SetupRefusal(started.Error.Code) ?? Labels.SetupMcpFailed);
            }
        }
        finally
        {
            _registering = false;
        }

        Fold();
    }

    /// <summary>Asks again, for every step, whether the app can do its action now.</summary>
    internal void Rederive()
    {
        foreach (var row in _rows.Values)
        {
            row.Rederive();
            row.Repo?.Refresh();
            row.Onyx?.Refresh();
            Confirm(row);
        }
    }

    /// <summary>Starts the map's refresh again (Retry on a failed map). One press sends once: the row shows as working until the next read, a refresh that is already running counts as running, and any other refusal is said in a sentence.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RefreshMap(CancellationToken cancellationToken)
    {
        if (_refreshing || _url is not { } url || !_rows.TryGetValue(MapStep, out var map) || map.IsWorking)   // one press sends once, whatever is in flight
        {
            return;
        }

        _refreshing = true;
        try
        {
            var started = await _api.RefreshContextMap(url, cancellationToken).ConfigureAwait(true);
            if (started.IsSuccess)
            {
                Hold(map, Labels.SetupMapAgain);
            }
            else if (IsRunningJob(started.Error))
            {
                Hold(map, Labels.SetupMapAlready);   // 409 with the running job: the same refresh is already going
            }
            else
            {
                map.Say(Labels.SetupMapRetryFailed);
            }
        }
        finally
        {
            _refreshing = false;
        }

        Fold();
    }

    // The row shows Working until the read that follows the send: a read begun before it is stale and must not bring the button back.
    private void Hold(SetupStepViewModel map, string note)
    {
        _holdUntil = _reads;
        map.ShowWorking(note);
    }

    // Only a 409 that names the running job means the same refresh is going; any other 409 (the job ended) is a refusal.
    private static bool IsRunningJob(Error error) =>
        string.Equals(error.Code, ConflictRefusal, StringComparison.Ordinal) && error.Extensions?.Any(e => string.Equals(e.Key, RunningJob, StringComparison.Ordinal)) == true;

    // Everything that follows from the steps: finished or not, the count and the strip, and whether Home shows its line.
    private void Fold()
    {
        IsFinished = Steps.Count > 0 && Steps.All(s => s.IsFinished);
        HasSteps = Steps.Count > 0;
        CountText = HasSteps ? Labels.SetupCount(Steps.Count(s => s.IsFinished), Steps.Count) : string.Empty;
        Lead = !HasSteps ? string.Empty : IsFinished ? Labels.SetupLeadDone : Labels.SetupLead;
        HasSub = HasSteps && !IsFinished;
        Segments = [.. Steps.Select(s => new SetupSegment(s.SegmentKind))];
        (MeterDone, MeterMax) = (Steps.Count(s => s.IsFinished), Steps.Count);
        FoldHomeLine();
        OnChanged(nameof(IsFootnoteShown));
    }

    // Home's line: shown while a step is open, naming the first step still to do, with Continue.
    private void FoldHomeLine()
    {
        IsHomeLineShown = !IsReading && !IsNotServed && !IsUnreadable && HasSteps && !IsFinished;
        var next = Steps.FirstOrDefault(s => !s.IsFinished);
        HomeStepText = next?.Title ?? string.Empty;
        ContinueText = next is null ? string.Empty : Labels.SetupContinue;
        OnChanged(nameof(HomeLineName));
        OnChanged(nameof(ContinueName));
    }
}
