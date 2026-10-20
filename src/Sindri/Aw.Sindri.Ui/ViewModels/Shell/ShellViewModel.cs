using System.ComponentModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The window (UI spec §3.2, §4.1): the top bar's navigation and health pill, the current page, the pre-API state, and
/// the detail sheet. It maps what the API and the platform say to words and visibility, and holds no other logic: the
/// connection and its state live in <see cref="Health"/>, and the window's size, the sheet's width and the last page are
/// local preferences, read once by <see cref="Restore"/> and written over the stored file when they change.
/// </summary>
internal sealed partial class ShellViewModel : ObservableObject
{
    /// <summary>How long without an event or a ping before the views are stale (§4.1, API §2.4).</summary>
    internal static readonly TimeSpan StaleAfter = HealthViewModel.StaleAfter;

    private static readonly string[] _stateProperties =
    [
        nameof(State), nameof(IsVersionMismatch), nameof(IsChromeVisible), nameof(IsNotRunning), nameof(IsNotRunningPanel), nameof(IsConnecting),
        nameof(IsUnreadable), nameof(IsNotRespondingBanner), nameof(IsPageVisible), nameof(IsInboxShown), nameof(IsPagesShown), nameof(IsSheetShown), nameof(IsEdgeTabShown), nameof(IsNavigationEnabled),
    ];

    private static readonly string[] _pageProperties =
    [
        nameof(CurrentPage), nameof(PageName), nameof(SelectedNav), nameof(IsHome), nameof(IsWorkflows), nameof(IsWork), nameof(IsRecipes), nameof(IsContext),
        nameof(IsAgents), nameof(IsSystem), nameof(IsJobs), nameof(IsSettings), nameof(IsNotRunningPanel), nameof(IsPageVisible), nameof(IsHomeShown), nameof(IsInboxShown), nameof(IsPagesShown),
    ];

    private readonly Preferences _store;
    private readonly TimeProvider _clock;
    private UiPreferences _preferences = UiPreferences.Defaults;

    // The page Settings was opened from, where leaving it over the Not running card returns.
    private PageId _beforeSettings = PageId.Home;

    /// <summary>The shell over its ports.</summary>
    /// <param name="records">Where the control plane recorded its endpoint.</param>
    /// <param name="api">Its API.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <param name="preferences">The app's local preferences, shared with Settings (the shell keeps its window, sheet width and last page there).</param>
    /// <param name="clipboard">Where Copy puts a CLI (the window's clipboard).</param>
    /// <param name="motion">The OS reduce-motion setting; none means motion is allowed.</param>
    /// <param name="window">The window's adapters (opening links and files, choosing a file); none opens and chooses nothing.</param>
    internal ShellViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock, Preferences preferences, IClipboard clipboard, IMotionPreference? motion = null, WindowPorts? window = null)
    {
        _store = preferences;
        _clock = clock;
        Health = new HealthViewModel(records, api, clock, clipboard);
        Health.PropertyChanged += OnHealthChanged;
        Health.Pill.PropertyChanged += OnPillChanged;
        CommandOutcome = new CommandOutcomeRegion(control => Health.Banner.HasRebuild && ReferenceEquals(Health.Banner.Rebuild, control));
        Create = new CreateItemViewModel(Health.Catalog, () => Health.ApiUrl(), api);
        Palette = new CommandPaletteViewModel(Health.Catalog, GoTo, RememberRun, CommandOutcome, () => Create.Open(), OpenStart, OpenApproveAll);
        Shortcuts = new ShortcutsViewModel(Health.Catalog);
        Queue = new QueueViewModel(api, Health.Catalog, clock);
        Queue.PageShown(RowScopeOf(CurrentPage));
        var decisions = new DecisionSender(api);
        Item = new ItemSheetViewModel(api, Health.Catalog, clock, decisions);
        Health.Follow(Queue.Refresh);
        Health.Follow(Item.Refresh);
        _api = api;
        NeedsYou = new NeedsYouViewModel(preferences, motion ?? new StillMotion(), clock);
        ApproveAll = new ApproveAllViewModel(Health.Catalog, () => Health.ApiUrl(), api, clock, () => NeedsYou.Seen.ReducedMotion);
        Decision = new DecisionSheetViewModel(api, () => Health.ApiUrl(), clock, decisions, clipboard);
        InitNeedsYou(clipboard);
        Inbox = InitInbox(clipboard);
        Strip = new WorkflowsStripViewModel(clipboard, clock);
        InitStrip();
        Workflows = new WorkflowsViewModel(api, clipboard, clock);
        InitWorkflows();
        Start = InitStart(api, window ??= WindowPorts.None);
        Instance = new InstanceFrameViewModel(api, clipboard, clock, catalog: Health.Catalog, launcher: window.Launcher);
        InitInstance();
        Recipe = new RecipeSheetViewModel(api);
        InitRecipe();
        InitAgents(clipboard);
        InitContext(api, clipboard);
        Queue.PropertyChanged += OnQueueChanged;
        InitAnnouncements();
        Attention = AttentionSummary.For(feed: null, State, _clock.GetUtcNow());
    }

    /// <summary>The app's copy, for the view.</summary>
    public ShellText Text { get; } = ShellText.Instance;

    /// <summary>The connection and the Health page's status card.</summary>
    public HealthViewModel Health { get; }

    /// <summary>The top bar's segments, in order: Home, Workflows, Work, Recipes, Context, Agents, System (screens spec §3).</summary>
    public IReadOnlyList<NavEntry> Navigation { get; } =
    [
        new(PageId.Home, Labels.Home, "H", string.Empty),
        new(PageId.Workflows, Labels.Workflows, "W", string.Empty),
        new(PageId.Work, Labels.Work, "Q", string.Empty),
        new(PageId.Recipes, Labels.Recipes, "R", string.Empty),
        new(PageId.Context, Labels.Context, "C", string.Empty),
        new(PageId.Agents, Labels.Agents, "A", string.Empty),
        new(PageId.System, Labels.System, ".", string.Empty),
    ];

    /// <summary>Every page the window can show, in order: the seven tabs, then Settings.</summary>
    public IReadOnlyList<PageId> Pages { get; } = Enum.GetValues<PageId>();

    /// <summary>The page shown.</summary>
    public PageId CurrentPage { get; private set; } = PageId.Home;

    /// <summary>The top bar's segment for the page shown; null on Settings. Setting it goes to its page.</summary>
    public NavEntry? SelectedNav
    {
        get => Navigation.FirstOrDefault(n => n.Page == CurrentPage);
        set
        {
            if (value is not null)
            {
                GoTo(value.Page);
            }
        }
    }

    /// <summary>The page region's accessible name: the page shown, in words.</summary>
    public string PageName => Labels.Page(CurrentPage);

    /// <summary>True on Home.</summary>
    public bool IsHome => CurrentPage == PageId.Home;

    /// <summary>True on Workflows.</summary>
    public bool IsWorkflows => CurrentPage == PageId.Workflows;

    /// <summary>True on Work.</summary>
    public bool IsWork => CurrentPage == PageId.Work;

    /// <summary>True on Recipes.</summary>
    public bool IsRecipes => CurrentPage == PageId.Recipes;

    /// <summary>True on Context.</summary>
    public bool IsContext => CurrentPage == PageId.Context;

    /// <summary>True on Agents.</summary>
    public bool IsAgents => CurrentPage == PageId.Agents;

    /// <summary>True on System.</summary>
    public bool IsSystem => CurrentPage == PageId.System;

    /// <summary>True on Settings.</summary>
    public bool IsSettings => CurrentPage == PageId.Settings;

    /// <summary>The window's pre-API state.</summary>
    public ShellState State => Health.State;

    /// <summary>The state's reason in words: why it isn't running, the two versions, the unreadable answer's code and message; empty when Normal.</summary>
    public string Reason => Health.Reason;

    /// <summary>The not-running reason's detail line (a path, an address, a pid and epoch), or empty.</summary>
    public string ReasonDetail => Health.ReasonDetail;

    /// <summary>The health pill's parts: its dot, glyph, word and age, and Refresh while stale.</summary>
    public HealthPill Pill => Health.Pill;

    /// <summary>Version mismatch: the full-window panel, and nothing else.</summary>
    public bool IsVersionMismatch => State == ShellState.VersionMismatch;

    /// <summary>The top bar shows in every state but a version mismatch.</summary>
    public bool IsChromeVisible => !IsVersionMismatch;

    /// <summary>The top bar's navigation takes input (dimmed and disabled while not running).</summary>
    public bool IsNavigationEnabled => !IsNotRunning;

    /// <summary>Not running: S1b's panel in place of the page.</summary>
    public bool IsNotRunning => State == ShellState.NotRunning;

    /// <summary>Not running: S1b's card, unless Settings (local preferences, reachable in any state) shows over it.</summary>
    public bool IsNotRunningPanel => IsNotRunning && !IsSettings;

    /// <summary>Connecting: skeleton rows in place of the page (first load only).</summary>
    public bool IsConnecting => State == ShellState.Connecting;

    /// <summary>Unreadable: the banner above the page, which keeps its last data.</summary>
    public bool IsUnreadable => State == ShellState.Unreadable;

    /// <summary>Stale after three failed refreshes: the banner under the top bar that says "Sindri not responding: run sindri doctor" (§4.1).</summary>
    public bool IsNotRespondingBanner => State == ShellState.Stale && Pill.IsNotResponding;

    /// <summary>The page shows when connected (Normal, Stale, or Unreadable with the last data), and Settings over the Not running card.</summary>
    public bool IsPageVisible => IsConnected || (IsSettings && IsNotRunning);

    // Connected: the API's pages (and the sheet over them) have something to show.
    private bool IsConnected => State is ShellState.Normal or ShellState.Stale or ShellState.Unreadable;

    /// <summary>The window's remembered bounds, or null to let the platform place it.</summary>
    public WindowBounds? Window => _preferences.Window;

    /// <summary>The detail sheet's width (420–760).</summary>
    public double SheetWidth => _preferences.SheetWidth;

    /// <summary>Shows a page (the G-chords, the top bar, the pill) and remembers it. Any way to a page but G D clears the Home filter.</summary>
    /// <param name="page">The page.</param>
    internal void GoTo(PageId page) => GoTo(page, HomeFilter.All);

    /// <summary>Shows a page with a Home filter, which is set once (G D is the one way to a filter but All).</summary>
    /// <param name="page">The page.</param>
    /// <param name="filter">The Home filter.</param>
    internal void GoTo(PageId page, HomeFilter filter)
    {
        if (page == PageId.Work)
        {
            Queue.Filter(QueueFilter.All);   // G Q, the palette and the tab show the whole queue; only GoToQueue narrows it
        }

        ShowPage(page, filter);
    }

    internal void ShowPage(PageId page, HomeFilter filter)
    {
        _ = LeaveNeedsYouInbox();   // any way to a page, Home included, leaves the inbox that shows over it
        SetHomeFilter(filter);
        if (page == CurrentPage)
        {
            return;
        }

        if (page == PageId.Settings)
        {
            _beforeSettings = CurrentPage;
        }

        CurrentPage = page;
        Workflows.ClearNote();
        Queue.PageShown(RowScopeOf(page));
        Changed(_pageProperties);
        Remember(p => p with { LastPage = page.ToString() });
    }

    /// <summary>
    /// Esc on Settings over the Not running card: back to the page it was opened from, so the card shows again. While
    /// connected Settings is a page like any other, and Esc leaves it where it is.
    /// </summary>
    /// <returns>True when it left Settings.</returns>
    internal bool LeaveSettings()
    {
        if (!IsSettings || !IsNotRunning)
        {
            return false;
        }

        GoTo(_beforeSettings);
        return true;
    }

    // LANE ANCHOR U3': new Needs you state belongs in its own partial (ShellViewModel.<Slice>.cs), not in this file (cap 300 lines); only a hook-up line goes here. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: new workflow state belongs in its own partial (ShellViewModel.<Slice>.cs), not in this file (cap 300 lines); only a hook-up line goes here. Insert immediately below this marker, never between the markers.

    /// <summary>Follows the control plane until cancelled (S1b's loop: control.json, ping, status, events).</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal Task Run(CancellationToken cancellationToken) => Health.Run(cancellationToken);

    private void OnHealthChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HealthViewModel.State):
                Changed(_stateProperties);
                Reattend();
                Announce();
                break;
            case nameof(HealthViewModel.Reason):
                OnChanged(e.PropertyName);
                Announce();
                break;
            case nameof(HealthViewModel.ReasonDetail):
                OnChanged(e.PropertyName);
                break;
            default:
                break;
        }
    }
}
