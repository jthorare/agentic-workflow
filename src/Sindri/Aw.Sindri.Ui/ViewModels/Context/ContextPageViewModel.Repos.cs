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
        OnChanged(nameof(SelectedRepo));
        FileMap.UseRepo(repo);
        CloseInspector();
        Whole.Unzoom();
        ForgetRead();
        OnChanged(nameof(IsZoomedIn));
        return ShowFiles ? FileMap.Opened(cancellationToken) : ReadWhole(cancellationToken);
    }

    // What the whole map says about the repos it covers; an answer that doesn't say leaves the list as it was.
    private void NoteRepos(Api.Context.WholeMapView answer)
    {
        if (answer.Repos is { Count: > 0 } repos)
        {
            Repos = [.. repos.Select(r => r.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            OnChanged(nameof(HasRepoPicker));
            OnChanged(nameof(SelectedRepo));
        }
    }
}
