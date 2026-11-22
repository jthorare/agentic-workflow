using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page (projects page spec §3, issue 3717): the owner's projects as rows in a view (keys 1 to 4), sorted and grouped (by health unless the owner
/// picks another), two folds, and the served sentence for an empty or unavailable list. It reads while it is the page shown, on every event, and keeps its
/// projects by id across reads. The server owns the order of Last activity, the health and its reason, and the sentence.
/// </summary>
internal sealed partial class ProjectsPageViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ProjectCardViewModel> _cards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectGroupViewModel> _groups = new(StringComparer.Ordinal);   // by grouping and key, so a group keeps its rows' controls across reads
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;

    internal ProjectsPageViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
    }

    /// <summary>The page's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.ProjectsPageTitle;

    /// <summary>"Reading projects…" until the first answer.</summary>
    public string ReadingText => Labels.ReadingProjects;

    /// <summary>The button that adds a project.</summary>
    public string AddText => Labels.ProjectsAddButton;

    /// <summary>The button that opens Connections to connect Linear.</summary>
    public string ConnectText => Labels.LinearConnect;
#pragma warning restore CA1822, MA0041

    /// <summary>True until the first answer.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The sources' freshness and the Linear viewer, joined with " · ".</summary>
    public string Freshness { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a source is failing: the line is tinted.</summary>
    public bool FreshnessFailing { get; private set => Set(ref field, value); }

    /// <summary>The served empty sentence, as served; null when none.</summary>
    public string? Sentence { get; private set => Set(ref field, value); }

    /// <summary>True when the sentence is about a missing Linear key or Linear not connected: the empty card or the banner offers Connect Linear.</summary>
    public bool ShowsConnect { get; private set => Set(ref field, value); }

    /// <summary>True when there are no cards and a sentence: the dashed block.</summary>
    public bool IsEmptyBlock { get; private set => Set(ref field, value); }

    /// <summary>True when there are cards and a sentence: the banner.</summary>
    public bool IsBanner { get; private set => Set(ref field, value); }

    /// <summary>True when Context is failing: cards show their name and origin chip only.</summary>
    public bool NamesOnly { get; private set => Set(ref field, value); }

    /// <summary>The groups of the view shown that have rows, in order.</summary>
    public ObservableCollection<ProjectGroupViewModel> Groups { get; } = [];

    /// <summary>Done and canceled projects.</summary>
    public ProjectFoldViewModel Finished { get; } = new(Labels.ProjectsFinishedTitle, Labels.ProjectsFinishedWhy);

    /// <summary>Projects the owner hid.</summary>
    public ProjectFoldViewModel Hidden { get; } = new(Labels.ProjectsHiddenTitle, string.Empty);

    /// <summary>True when the page is wide enough for the Agents column.</summary>
    public bool ShowsAgents { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the page is wide enough for the Items and Target columns and the health reason; narrower, a row keeps its name, PRs, Blocked on and Active.</summary>
    public bool ShowsWide { get; private set => Set(ref field, value); } = true;

    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>A card asked for its sheet (a click, Enter).</summary>
    internal event EventHandler<ProjectOpenRequestedEventArgs>? OpenRequested;

    /// <summary>A write closed a sheet: the view moves focus.</summary>
    internal event EventHandler<ProjectFocusEventArgs>? FocusRequested;

    /// <summary>Add project was pressed: the shell opens the Add project sheet.</summary>
    internal event EventHandler? AddRequested;

    /// <summary>Asks the shell to open the Add project sheet.</summary>
    internal void RequestAdd() => AddRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the shell to open a card's sheet.</summary>
    /// <param name="link">The card's link.</param>
    internal void RequestOpen(DeepLink link) => OpenRequested?.Invoke(this, new ProjectOpenRequestedEventArgs(link));

    /// <summary>Re-reads the list after a write took a card off the page (or back), then asks the view to move focus: to the next card, else the previous, else Add project; an unhidden project's fold.</summary>
    /// <param name="done">What the write did.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown and focus is asked for.</returns>
    internal async Task Settled(ProjectDoneEventArgs done, CancellationToken cancellationToken)
    {
        var order = InReadingOrder().Select(c => c.Id).ToList();
        var at = order.IndexOf(done.Id);
        var candidates = at < 0 ? [] : order.Skip(at + 1).Take(1).Concat(order.Take(at).Reverse().Take(1)).ToList();   // the next card, else the previous
        if (_url is { } url)
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }

        FocusRequested?.Invoke(this, done.Kind switch
        {
            ProjectDone.Unhidden => Fold(done.WentToFinished),
            ProjectDone.Added => Card([done.Id], gone: string.Empty),   // the new card, else Add project when a fold hides it
            ProjectDone.Removed or ProjectDone.Hidden => Card(candidates, done.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(done), done.Kind, message: null),
        });
    }

    // The cards as the page lays them out: the groups, then the folds that are open.
    private IEnumerable<ProjectCardViewModel> InReadingOrder() =>
        Groups.SelectMany(g => g.Cards).Concat(Finished.IsOpen ? Finished.Cards : []).Concat(Hidden.IsOpen ? Hidden.Cards : []);

    // The first neighbour still on the page, else Add project.
    private ProjectFocusEventArgs Card(List<string> candidates, string gone) =>
        candidates.FirstOrDefault(id => !string.Equals(id, gone, StringComparison.Ordinal) && InReadingOrder().Any(c => string.Equals(c.Id, id, StringComparison.Ordinal))) is { } id
            ? new ProjectFocusEventArgs(ProjectFocus.Card, id)
            : new ProjectFocusEventArgs(ProjectFocus.Add, cardId: null);

    // An unhidden project's fold button, while it still has cards, else Add project.
    private ProjectFocusEventArgs Fold(bool finished) =>
        (finished ? Finished : Hidden).IsVisible
            ? new ProjectFocusEventArgs(finished ? ProjectFocus.FinishedFold : ProjectFocus.HiddenFold, cardId: null)
            : new ProjectFocusEventArgs(ProjectFocus.Add, cardId: null);

    /// <summary>The page's row of a project, by id, for the sheet's notes; null when the page holds none.</summary>
    /// <param name="id">The project's id.</param>
    /// <returns>The row, or null.</returns>
    internal ProjectCardViewModel? CardOf(string id) => _cards.GetValueOrDefault(id);

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>The page's width changed: from 868 px every column, from 808 every one but Agents, narrower the name, PRs, Blocked on and Active.</summary>
    /// <param name="width">The width in px.</param>
    internal void SetPageWidth(double width)
    {
        ShowsAgents = width >= Labels.ProjectsAgentsFrom;
        ShowsWide = width >= Labels.ProjectsWideFrom;
    }

    /// <summary>Re-reads the list (on connect and on every event); while the page isn't shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden; becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Projects page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            _reads++;   // a read already on its way is not wanted
            Dispose();
        }
    }

    /// <summary>Cancels the read started when the page was shown.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    // The read made when the page is shown, awaited by CatchUp so nothing about it is unobserved: a leave cancels it, any other failure is drawn as a failed read.
    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await _api.Projects(url, all: false, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        IsLoading = false;   // the last good answer stays under the line
        Error = Labels.ProjectsFailed(error);
    }

    private void Show(ProjectList list)
    {
        (IsLoading, Error) = (false, string.Empty);
        var failing = list.Freshness.Any(f => string.Equals(f.State, "failing", StringComparison.Ordinal));
        FreshnessFailing = failing;
        Freshness = ProjectsLinear.Freshness(list);
        NamesOnly = list.Freshness.Any(f => string.Equals(f.Source, "context", StringComparison.Ordinal) && string.Equals(f.State, "failing", StringComparison.Ordinal));
        (Sentence, ShowsConnect) = (ProjectsLinear.Sentence(list, _clock.GetUtcNow()), ProjectsLinear.OffersConnect(list));
        IsEmptyBlock = list.Projects.Count == 0 && list.Empty is not null;
        IsBanner = list.Projects.Count > 0 && list.Empty is not null;
        FactsMissing = ProjectRows.FactsMissing(list);
        _list = list;

        var present = new HashSet<string>(list.Projects.Concat(list.Finished).Concat(list.Hidden).Select(c => c.Id), StringComparer.Ordinal);
        foreach (var gone in _cards.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _cards.Remove(gone);
        }

        Arrange();
    }

    private ProjectCardViewModel Card(ProjectCardView served)
    {
        if (_cards.TryGetValue(served.Id, out var kept))
        {
            kept.Update(served);
        }
        else
        {
            kept = _cards[served.Id] = new ProjectCardViewModel(served, _clock, _api, () => _url);
        }

        kept.NamesOnly = NamesOnly;
        kept.FactsMissing = FactsMissing;
        return kept;
    }
}
