using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>The Work toolbar (<see cref="QueueViewModel"/>, continued): the All and Running segments with their counts, and the repo chip.</summary>
internal sealed partial class QueueViewModel
{
    private const string RunningGroup = "running";

    /// <summary>The All segment's words with the count of every item.</summary>
    public string AllSegment { get; private set => Set(ref field, value); } = Labels.QueueAllSegment(0);

    /// <summary>The Running segment's words with the count of running items.</summary>
    public string RunningSegment { get; private set => Set(ref field, value); } = Labels.QueueRunningSegment(0);

    /// <summary>The repos the items name, behind "All repos".</summary>
    public ObservableCollection<string> RepoChoices { get; } = [Labels.AllRepos];

    /// <summary>The repo the rows are narrowed to; empty for all.</summary>
    public string SelectedRepo { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the Running segment is the one chosen.</summary>
    public bool IsRunningSegment => Showing == QueueFilter.Running;

    /// <summary>Narrows the rows to a repo (or back to all).</summary>
    /// <param name="repo">The repo, or "All repos".</param>
    internal void ChooseRepo(string repo)
    {
        SelectedRepo = string.Equals(repo, Labels.AllRepos, StringComparison.Ordinal) ? string.Empty : repo;
        Show();
        ClearHidden();
    }

    private void Toolbar(Dictionary<string, List<ItemRowViewModel>> all)
    {
        AllSegment = Labels.QueueAllSegment(all.Values.Sum(r => r.Count));
        RunningSegment = Labels.QueueRunningSegment(all.GetValueOrDefault(RunningGroup)?.Count ?? 0);
        var repos = all.Values.SelectMany(r => r).Select(r => r.Repo).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Patch.To(RepoChoices, [Labels.AllRepos, .. repos]);
        OnChanged(nameof(IsRunningSegment));
    }

    private bool RepoMatches(string repo) => SelectedRepo.Length == 0 || string.Equals(repo, SelectedRepo, StringComparison.Ordinal);
}
