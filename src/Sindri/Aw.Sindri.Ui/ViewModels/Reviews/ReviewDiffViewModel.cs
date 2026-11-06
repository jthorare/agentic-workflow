namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff, the page's right pane (review page spec §6.2, §6.3): its state and words, the file count and the folded or open groups.</summary>
internal sealed class ReviewDiffViewModel : ObservableObject
{
    /// <summary>Loading, ready, empty or error.</summary>
    public ReviewPanelState State { get; } = ReviewPanelState.Loading;

    /// <summary>The words of a loading, empty or error diff: "Reading files…", "This PR changes no files", "Couldn't read the diff: …".</summary>
    public string Text { get; } = string.Empty;

    /// <summary>"4 files", or "3,000 files" at GitHub's cap.</summary>
    public string CountText { get; } = string.Empty;

    /// <summary>"GitHub lists the first 3,000 files; open the rest on GitHub" at the cap; empty otherwise.</summary>
    public string CappedText { get; } = string.Empty;

    /// <summary>The groups, in order.</summary>
    public IReadOnlyList<ReviewDiffGroupViewModel> Groups { get; } = [];
}
