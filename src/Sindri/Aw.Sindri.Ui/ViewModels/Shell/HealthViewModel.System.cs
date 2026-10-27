using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The System page's two tabs, its headline tone, problem lines and cards (<see cref="HealthViewModel"/>, continued; system spec §2).</summary>
internal sealed partial class HealthViewModel
{
    // The views' freshness and the services as last read (null when the view is not served or failed).
    private ServiceList? _serviceList;
    private readonly Dictionary<string, ServiceRowViewModel> _serviceRows = new(StringComparer.Ordinal);

    /// <summary>The first tab's name.</summary>
    public string OverviewTabLabel { get; } = Labels.SystemOverviewTab;

    /// <summary>The tabs' accessible name.</summary>
    public string TabsName { get; } = Labels.SystemTabsName;

    /// <summary>The second tab's name with the number of services: "Services (6)".</summary>
    public string ServicesTabLabel { get; private set => Set(ref field, value); } = Labels.ServicesTab(0);

    /// <summary>The Services card's link: "6 scheduled".</summary>
    public string ServicesScheduled { get; private set => Set(ref field, value); } = Labels.ServicesScheduled(0);

    /// <summary>Whether the Services tab is the one shown (else Overview).</summary>
    public bool ShowServicesTab
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(ShowServicesTab));
            OnChanged(nameof(ShowOverview));
        }
    }

    /// <summary>Whether Overview is shown (the segment sets it; choosing Overview is the only thing it can do).</summary>
    public bool ShowOverview
    {
        get => !ShowServicesTab;
        set
        {
            if (value)
            {
                ShowServicesTab = false;
            }
        }
    }

    /// <summary>True under the page width where the Services table keeps three columns.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when every column shows.</summary>
    public bool IsWide => !IsCompact;

    /// <summary>Raised when a row's View job is pressed; the shell opens the Jobs page on that job's sheet.</summary>
    internal event EventHandler<JobRequestedEventArgs>? JobRequested;

    /// <summary>The page's width changed: under 900 px the Services table keeps three columns.</summary>
    /// <param name="width">The page's width in px.</param>
    internal void Resize(double width)
    {
        var compact = width < Labels.SystemWideFrom;
        if (compact == IsCompact)
        {
            return;
        }

        IsCompact = compact;
        OnChanged(nameof(IsWide));
        foreach (var row in _serviceRows.Values)
        {
            row.SetCompact(compact);
        }
    }

    /// <summary>Left or Right on the focused tab: moves to the neighbouring tab.</summary>
    /// <param name="step">-1 for Left, 1 for Right.</param>
    /// <returns>True when the other tab is now shown (false at an end).</returns>
    internal bool MoveTab(int step)
    {
        var target = step > 0;
        if (target == ShowServicesTab)
        {
            return false;
        }

        ShowServicesTab = target;
        return true;
    }

    /// <summary>The Services table's columns.</summary>
    public IReadOnlyList<string> ServiceColumns { get; } = [Labels.ServiceColumnName, Labels.ServiceColumnEvery, Labels.ServiceColumnStatus, Labels.ServiceColumnWhat, Labels.ServiceColumnLastRun];

    /// <summary>Every scheduled service, in the API's order.</summary>
    public IReadOnlyList<ServiceRowViewModel> ServiceTable { get; private set => Set(ref field, value); } = [];

    /// <summary>The first services, for the Overview's card.</summary>
    public IReadOnlyList<ServiceRowViewModel> ServiceCard { get; private set => Set(ref field, value); } = [];

    /// <summary>The heading over the problem lines.</summary>
    public string NeedsALookHeading { get; } = Labels.SystemNeedsALook;

    /// <summary>What is wrong, one line each (code and words as Sindri served them).</summary>
    public IReadOnlyList<HealthProblemView> ProblemLines { get; private set => Set(ref field, value); } = [];

    /// <summary>True when any problem is listed.</summary>
    public bool HasProblemLines => ProblemLines.Count > 0;

    /// <summary>The headline is amber: something needs a look, or the profile needs approval.</summary>
    public bool ToneWarn { get; private set => Set(ref field, value); }

    /// <summary>The headline is red: stalled, not running, unreadable or a version mismatch.</summary>
    public bool ToneError { get; private set => Set(ref field, value); }

    /// <summary>The Views card's name.</summary>
    public string ViewsCardName { get; } = Labels.ViewsCard;

    /// <summary>The Views card's state: Up to date, Rebuilding or Stalled.</summary>
    public string ViewsWord { get; private set => Set(ref field, value); } = Labels.ViewsUnknown;

    /// <summary>The Views card's rows: read up to, updated.</summary>
    public IReadOnlyList<HealthCell> ViewsRows { get; private set => Set(ref field, value); } = [];

    /// <summary>Rebuild views, when the views are stalled or rebuilding; null otherwise.</summary>
    public ControlViewModel? ViewsFix { get; private set => Set(ref field, value); }

    /// <summary>The Agents card's name.</summary>
    public string AgentsCardName { get; } = Labels.AgentsCard;

    /// <summary>The Agents card's rows: running, asking you, problems.</summary>
    public IReadOnlyList<HealthCell> AgentsRows { get; private set => Set(ref field, value); } = [];

    // The headline's tone, the problem lines and the Views and Agents cards, from what the last status and services read said.
    private void ShowSystemState()
    {
        var look = NeedsLook();
        Sentence = SentenceFor(new SentenceInputs(State, _health is not null, Pill.IsHealthy, Pill.Problems, HasProfileFix, _health?.Sentence, look));
        (ToneWarn, ToneError) = ToneFor(State, _health?.Word, HasProfileFix || look.Length > 0);
        ProblemLines = _health?.Problems ?? [];
        OnChanged(nameof(HasProblemLines));
        ShowAgentsCard();
        ShowViewsCard();
        ShowStateBox();
    }

    // (warn, error): red when the control plane cannot be read or is stalled; amber when it needs a look; else none.
    private static (bool Warn, bool Error) ToneFor(ShellState state, string? word, bool profileFix) => state switch
    {
        ShellState.Normal => NormalTone(word, profileFix),
        ShellState.Stale => (true, false),
        ShellState.NotRunning or ShellState.Unreadable or ShellState.VersionMismatch => (false, true),
        ShellState.Connecting => (false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, message: null),
    };

    private static (bool Warn, bool Error) NormalTone(string? word, bool profileFix) => word switch
    {
        null or "rebuilding" => (false, false),
        "stalled" => (false, true),
        "healthy" => (profileFix, false),
        _ => (true, false),
    };

    /// <summary>Shows the services read (API P2): the Services tab's table and the Overview's card.</summary>
    /// <param name="view">The view, or null when it is not served or failed.</param>
    private void ShowServices(ServiceList? view)
    {
        _serviceList = view;
        var rows = view?.Services ?? [];
        var names = rows.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var gone in _serviceRows.Keys.Where(k => !names.Contains(k)).ToList())
        {
            _ = _serviceRows.Remove(gone);
        }

        var shown = new List<ServiceRowViewModel>();
        foreach (var row in rows)
        {
            if (!_serviceRows.TryGetValue(row.Name, out var vm))
            {
                vm = _serviceRows[row.Name] = new ServiceRowViewModel(row.Name);
                vm.SetCompact(IsCompact);   // a row that arrives after the page narrowed is narrow too
                vm.JobRequested += (_, e) => JobRequested?.Invoke(this, e);
            }

            vm.Show(row);
            WireServiceControls(vm, row);
            shown.Add(vm);
        }

        ServiceTable = shown;
        ServiceCard = [.. shown];
        ServicesTabLabel = Labels.ServicesTab(shown.Count);
        ServicesScheduled = Labels.ServicesScheduled(shown.Count);
        lock (_gate)
        {
            ShowSystemState();
        }
    }

    // Run now stays one control for the row's life; the toggle is Pause until the service is paused, then Resume.
    private void WireServiceControls(ServiceRowViewModel row, ServiceRowView view)
    {
        if (_url is not { } url)
        {
            return;
        }

        var paused = view.Paused;
        var allowed = view.Actions;
        row.Run ??= Catalog.On("RunService", row.Name, async ct => (await api.RunService(url, row.Name, ct).ConfigureAwait(true)).Map(started => started.Accepted));
        row.RunAllowed = Allows(allowed, "run");
        row.ToggleAllowed = Allows(allowed, paused ? "resume" : "pause");
        if (row.Toggle is null || row.ToggleResumes != paused)
        {
            row.ToggleResumes = paused;
            row.Toggle = paused
                ? Catalog.On("ResumeService", row.Name, ct => api.ResumeService(url, row.Name, ct))
                : Catalog.On("PauseService", row.Name, ct => api.PauseService(url, row.Name, ct));
        }
    }

    // A served list of allowed actions decides; an answer without one allows all (the server's refusal is then shown on the row).
    private static bool Allows(IReadOnlyList<string>? allowed, string action) => allowed is null || allowed.Contains(action, StringComparer.Ordinal);

    // A catalog read again: the rows' controls belong to the old one, so they are made again from the services as last read.
    private void ReconnectServiceControls()
    {
        foreach (var row in _serviceRows.Values)
        {
            (row.Run, row.Toggle) = (null, null);
        }

        ShowServices(_serviceList);
    }
}
