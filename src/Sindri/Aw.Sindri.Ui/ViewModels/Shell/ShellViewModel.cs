using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Setup;
using Aw.Sindri.Ui.ViewModels.Todo;

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
        nameof(CurrentPage), nameof(PageName), nameof(SelectedNav), nameof(IsHome), nameof(IsProjects), nameof(IsWork), nameof(IsContext),
        nameof(IsAgents), nameof(IsSystem), nameof(IsReviews), nameof(IsConnections), nameof(IsJobs), nameof(IsMemory), nameof(IsResources), nameof(IsWorkflows), nameof(IsSettings), nameof(IsNotRunningPanel), nameof(IsPageVisible), nameof(IsHomeShown), nameof(IsInboxShown), nameof(IsPagesShown),
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
        Start = new SindriStartViewModel((window ?? WindowPorts.None).Starter, clock, (window ?? WindowPorts.None).Log);
        _store = preferences;
        _clock = clock;
        Health = new HealthViewModel(records, api, clock, clipboard);
        Health.PropertyChanged += OnHealthChanged;
        Health.Pill.PropertyChanged += OnPillChanged;
        CommandOutcome = new CommandOutcomeRegion(control => Health.Banner.HasRebuild && ReferenceEquals(Health.Banner.Rebuild, control));
        Create = new CreateItemViewModel(Health.Catalog, () => Health.ApiUrl(), api);
        QuickAdd = new QuickAddViewModel(api, () => Health.ApiUrl(), clock);
        Palette = new CommandPaletteViewModel(Health.Catalog, GoTo, RememberRun, CommandOutcome, () => Create.Open(), openTodo: OpenTodo, newTodo: () => OpenQuickAdd()) { OpenSetting = OpenSettingFilter };
        Shortcuts = new ShortcutsViewModel(Health.Catalog);
        Queue = new QueueViewModel(api, Health.Catalog, clock);
        Queue.PageShown(RowScopeOf(CurrentPage));
        var decisions = new DecisionSender(api);
        Item = new ItemSheetViewModel(api, Health.Catalog, clock, decisions);
        _api = api;
        FollowViews();
        NeedsYou = new NeedsYouViewModel(preferences, motion ?? new StillMotion(), clock);
        Decision = new DecisionSheetViewModel(api, () => Health.ApiUrl(), clock, decisions, clipboard);
        InitNeedsYou(clipboard);
        Inbox = InitInbox(api, clipboard, (window ?? WindowPorts.None).Launcher);
        InitSandbox();
        window ??= WindowPorts.None;
        (Setup, LibraryRoots, LinearConnection) = (new SetupViewModel(api, Health.Catalog, clock, OpensInApp, window.Folders), InitLibraryRoots(api, window), InitLinearConnection(api));
        InitSetup();
        InitSheetNames();
        InitAgents(clipboard, window.Launcher);
        InitContext(api, clipboard, window.Launcher);
        Queue.PropertyChanged += OnQueueChanged;
        Create.Created += (_, created) => Queue.Reveal(created.ItemId);
        InitAnnouncements();
        Attention = AttentionSummary.For(feed: null, State, _clock.GetUtcNow());
    }

    // The views the control plane's follower re-reads on connect and after every event.
    [MemberNotNull(nameof(Linear), nameof(NewIssue), nameof(Todo), nameof(Elsewhere), nameof(Toast))]
    private void FollowViews()
    {
        InitTodo();
        InitElsewhere();
        InitToast();
        WireQuickAddToasts();
        InitLinear();
        Health.Follow(Queue.Refresh);
        Health.Follow(Item.Refresh);
        Health.FollowCards();
    }

    /// <summary>The app's copy, for the view.</summary>
    public ShellText Text { get; } = ShellText.Instance;

    /// <summary>The connection and the Health page's status card.</summary>
    public HealthViewModel Health { get; }

    /// <summary>The top bar's segments, in order: Home, Projects, Workflows, Work, Context, Agents, System, Reviews, Connections (screens spec §3, Projects page spec §7).</summary>
    public IReadOnlyList<NavEntry> Navigation { get; } =
    [
        new(PageId.Home, Labels.Home, "H", string.Empty),
        new(PageId.Projects, Labels.Projects, "P", string.Empty),
        new(PageId.Workflows, Labels.Workflows, "W", string.Empty),
        new(PageId.Work, Labels.Work, "Q", string.Empty),
        new(PageId.Context, Labels.Context, "C", string.Empty),
        new(PageId.Agents, Labels.Agents, "A", string.Empty),
        new(PageId.System, Labels.System, ".", string.Empty),
        new(PageId.Reviews, Labels.ReviewsTab, "V", string.Empty),
        new(PageId.Connections, Labels.ConnectionsPage, "O", string.Empty),
    ];

    /// <summary>Every page the window can show, in order: the tabs, then Jobs, Memory and Settings.</summary>
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
                IsSelectingTab = true;
                GoTo(value.Page);
                IsSelectingTab = false;
            }
        }
    }

    /// <summary>The page region's accessible name: the page shown, in words.</summary>
    public string PageName => Labels.Page(CurrentPage);

    /// <summary>True on Home.</summary>
    public bool IsHome => CurrentPage == PageId.Home;

    /// <summary>True on Work.</summary>
    public bool IsWork => CurrentPage == PageId.Work;

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

    /// <summary>The Not running page's start states: Starting, Didn't start, Already starting, a broken install.</summary>
    public SindriStartViewModel Start { get; }

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
        OpenPage(page);
    }

    // What a page does when it is entered: GoTo runs it, and so does Restore for the page the app starts on.
    private void OpenPage(PageId page)
    {
        if (page == PageId.Settings)
        {
            ReadLibraryRoots();
            Setup.RevealChecklist();   // every way into Settings (the rail, ⌘,, G U, the setup link, Home's line) shows the checklist, its first section
            ReadLinearConnection();   // after the checklist's reveal, so a Connect that asked for the Linear card is the last scroll
            ReadTodoNoteCard();
        }

        if (page == PageId.Context)
        {
            _ = Context.Opened(CancellationToken.None);   // the page opens on the whole map, read here with no starting point
        }
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
        else if (CurrentPage == PageId.Settings)
        {
            LinearConnection.Leave();   // the click's own "Saved." goes with the page, so the next open shows what is true then
        }

        CurrentPage = page;
        Start.ShowPage(PageName);
        Queue.PageShown(RowScopeOf(page));
        Changed(_pageProperties);
        Remember(p => p with { LastPage = page.ToString() });
    }

    // LANE ANCHOR U3': new Needs you state belongs in its own partial (ShellViewModel.<Slice>.cs), not in this file (cap 300 lines); only a hook-up line goes here. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: new workflow state belongs in its own partial (ShellViewModel.<Slice>.cs), not in this file (cap 300 lines); only a hook-up line goes here. Insert immediately below this marker, never between the markers.

    /// <summary>Follows the control plane until cancelled (S1b's loop: control.json, ping, status, events).</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal Task Run(CancellationToken cancellationToken) => Health.Run(cancellationToken);
}
