using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The repo picker (<see cref="ContextPageViewModel"/>, continued): when the map covers more than one repo, each has its own top ("~") and the page reads one at a time.</summary>
internal sealed partial class ContextPageViewModel
{
    private string? _repo;

    /// <summary>The picker's name.</summary>
    public string RepoName { get; } = Labels.ContextRepoName;

    /// <summary>The repos the map covers, by name; empty until the map says, which reads as one repo.</summary>
    public IReadOnlyList<string> Repos { get; private set => Set(ref field, value); } = [];

    /// <summary>Whether the picker shows: only when the map covers two repos or more.</summary>
    public bool HasRepoPicker => Repos.Count > 1;

    /// <summary>The repo being read; choosing another reads that repo's map from its top.</summary>
    public string? SelectedRepo
    {
        get => _repo ?? (Repos.Count > 0 ? Repos[0] : null);
        set
        {
            if (value is not null)
            {
                _ = PickRepo(value, CancellationToken.None);
            }
        }
    }

    /// <summary>Reads another repo: the answer on the page and the zoom go, the whole map and the Files view start at that repo's top.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the map is drawn.</returns>
    internal Task PickRepo(string repo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (string.Equals(repo, SelectedRepo, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _repo = repo;
        _openGeneration++;   // a first read of the other repo still in flight is dropped when it ends (#3697)
        OnChanged(nameof(SelectedRepo));
        FileMap.UseRepo(repo);
        CloseInspector();
        Whole.Unzoom();
        ForgetRead();
        OnChanged(nameof(IsZoomedIn));
        Whole.LeaveConcept();   // the concepts are another repo's now
        OpenedConceptId = null;
        _conceptsRead = false;   // the other repo's concepts are not read yet, so its first event reads them before its whole map (#3792)
        Concepts.Reset();
        ChangedConcepts();
        return ShowFiles ? FileMap.Opened(cancellationToken) : OpenFirst(cancellationToken);
    }

    // What the whole map or the concepts answer says about the repos the map covers (#3696: a page that opens on the concepts reads no whole map); an answer that doesn't
    // say leaves the list as it was.
    private void NoteRepos(IReadOnlyList<Api.Context.WholeRepoSummary>? listed)
    {
        if (listed is { Count: > 0 } repos)
        {
            Repos = [.. repos.Select(r => r.Name).Distinct(StringComparer.Ordinal).Order(NaturalOrder.Instance)];
            OnChanged(nameof(HasRepoPicker));
            OnChanged(nameof(SelectedRepo));
        }
    }
}
