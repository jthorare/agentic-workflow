namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Waiting on you or Yours (review page spec §6.1): its heading, row count and sections.</summary>
internal sealed class ReviewGroupViewModel
{
    /// <summary>"Waiting on you" or "Yours".</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The group's rows, over all its sections.</summary>
    public int Count { get; private set; }

    /// <summary>True for Waiting on you (its heading is drawn in the attention color).</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>By project first, then by repo name.</summary>
    public IReadOnlyList<ReviewSectionViewModel> Sections { get; private set; } = [];
}
