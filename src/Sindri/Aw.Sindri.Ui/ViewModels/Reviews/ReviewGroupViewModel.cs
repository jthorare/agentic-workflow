using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Waiting on you or Yours (review page spec §6.1): its heading, row count and sections.</summary>
internal sealed class ReviewGroupViewModel
{
    internal ReviewGroupViewModel(ReviewGroupView group, string? login, DateTimeOffset now)
    {
        Title = group.Title;
        IsWaiting = string.Equals(group.Kind, "waiting", StringComparison.Ordinal);
        Sections = [.. group.Sections.Select(s => new ReviewSectionViewModel(s, login, now))];
        Count = Sections.Sum(s => s.Rows.Count);
    }

    /// <summary>"Waiting on you" or "Yours".</summary>
    public string Title { get; }

    /// <summary>The group's rows, over all its sections.</summary>
    public int Count { get; }

    /// <summary>True for Waiting on you (its heading is drawn in the attention color).</summary>
    public bool IsWaiting { get; }

    /// <summary>By project first, then by repo name.</summary>
    public IReadOnlyList<ReviewSectionViewModel> Sections { get; }
}
