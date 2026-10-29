#pragma warning disable MA0182 // the shell wires the page in a later step
using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page (projects page spec §3): the owner's projects in state groups, two folds, and the served sentence for an empty or unavailable list.
/// It reads while it is the page shown, on every event, and keeps its cards by id across reads. The server owns the order and the sentence.
/// </summary>
internal sealed class ProjectsPageViewModel : ObservableObject, IDisposable
{
    private static readonly string[] _stateOrder = ["inProgress", "planned", "paused", "backlog"];

    private readonly Dictionary<string, ProjectCardViewModel> _cards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectGroupViewModel> _groups = new(StringComparer.Ordinal);
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
#pragma warning restore CA1822, MA0041

    /// <summary>True until the first answer.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The sources' freshness and the Linear viewer, joined with " · ".</summary>
    public string Freshness { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a source is failing: the line is tinted.</summary>
    public bool FreshnessFailing { get; private set => Set(ref field, value); }

    /// <summary>The served empty sentence, as served; null when none.</summary>
    public string? Sentence { get; private set => Set(ref field, value); }

    /// <summary>True when there are no cards and a sentence: the dashed block.</summary>
    public bool IsEmptyBlock { get; private set => Set(ref field, value); }

    /// <summary>True when there are cards and a sentence: the banner.</summary>
    public bool IsBanner { get; private set => Set(ref field, value); }

    /// <summary>True when the dashed block offers Add project (not while Linear is syncing).</summary>
    public bool ShowsAddInEmpty { get; private set => Set(ref field, value); }

    /// <summary>True when Context is failing: cards show their name and origin chip only.</summary>
    public bool NamesOnly { get; private set => Set(ref field, value); }

    /// <summary>The state groups that have cards, in order.</summary>
    public ObservableCollection<ProjectGroupViewModel> Groups { get; } = [];

    /// <summary>Done and canceled projects.</summary>
    public ProjectFoldViewModel Finished { get; } = new(Labels.ProjectsFinishedTitle, Labels.ProjectsFinishedWhy);

    /// <summary>Projects the owner hid.</summary>
    public ProjectFoldViewModel Hidden { get; } = new(Labels.ProjectsHiddenTitle, string.Empty);

    /// <summary>How many cards sit across: 1, 2 or 3.</summary>
    public int Columns { get; private set => Set(ref field, value); } = 1;

    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>The page's width changed: 2 columns from 560 px, 3 from 1,040.</summary>
    /// <param name="width">The width in px.</param>
    internal void SetPageWidth(double width) =>
        Columns = width >= Labels.ProjectsThreeColumnsFrom ? 3 : width >= Labels.ProjectsTwoColumnsFrom ? 2 : 1;

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
        Freshness = string.Join(" · ", list.Freshness.Select(f => Labels.ProjectSource(f.Source) + " " + f.Text).Concat(list.Viewer is { } viewer ? [Labels.ProjectViewer(viewer)] : []));
        NamesOnly = list.Freshness.Any(f => string.Equals(f.Source, "context", StringComparison.Ordinal) && string.Equals(f.State, "failing", StringComparison.Ordinal));
        Sentence = list.Empty;
        IsEmptyBlock = list.Projects.Count == 0 && list.Empty is not null;
        IsBanner = list.Projects.Count > 0 && list.Empty is not null;
        ShowsAddInEmpty = IsEmptyBlock && !list.Freshness.Any(f => string.Equals(f.State, "syncing", StringComparison.Ordinal));

        var present = new HashSet<string>(list.Projects.Concat(list.Finished).Concat(list.Hidden).Select(c => c.Id), StringComparer.Ordinal);
        foreach (var gone in _cards.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _cards.Remove(gone);
        }

        ShowGroups(list.Projects);
        Finished.Show([.. list.Finished.Select(Card)]);
        Hidden.Show([.. list.Hidden.Select(Card)]);
    }

    // One group per state that has cards: the four known states in order, then any other state in the order the server first serves it.
    private void ShowGroups(IReadOnlyList<ProjectCardView> projects)
    {
        var byState = projects.GroupBy(p => p.State ?? string.Empty, StringComparer.Ordinal).ToList();
        var ordered = _stateOrder.SelectMany(s => byState.Where(g => string.Equals(g.Key, s, StringComparison.Ordinal)))
            .Concat(byState.Where(g => !_stateOrder.Contains(g.Key, StringComparer.Ordinal)))
            .ToList();
        var groups = new List<ProjectGroupViewModel>();
        foreach (var served in ordered)
        {
            var heading = Labels.ProjectStateWord(served.Key);
            if (!_groups.TryGetValue(heading, out var group))
            {
                group = _groups[heading] = new ProjectGroupViewModel(heading);
            }

            group.Show([.. served.Select(Card)]);
            groups.Add(group);
        }

        Patch.To(Groups, groups);
    }

    private ProjectCardViewModel Card(ProjectCardView served)
    {
        if (_cards.TryGetValue(served.Id, out var kept))
        {
            kept.Update(served);
        }
        else
        {
            kept = _cards[served.Id] = new ProjectCardViewModel(served, _clock);
        }

        kept.NamesOnly = NamesOnly;
        return kept;
    }
}
