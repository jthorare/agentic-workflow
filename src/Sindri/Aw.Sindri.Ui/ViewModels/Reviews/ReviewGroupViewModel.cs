using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Waiting on you or Yours (review page spec §6.1): its heading, row count and sections. A refresh updates it in place.</summary>
internal sealed class ReviewGroupViewModel : ObservableObject
{
    internal ReviewGroupViewModel(string kind, string title)
    {
        Title = title;
        IsWaiting = string.Equals(kind, "waiting", StringComparison.Ordinal);
    }

    /// <summary>"Waiting on you" or "Yours".</summary>
    public string Title { get; }

    /// <summary>The group's rows, over all its sections.</summary>
    public int Count { get; private set => Set(ref field, value); }

    /// <summary>True for Waiting on you (its heading is drawn in the attention color).</summary>
    public bool IsWaiting { get; }

    /// <summary>By project first, then by repo name.</summary>
    public ObservableCollection<ReviewSectionViewModel> Sections { get; } = [];

    /// <summary>Counts the rows after the sections were patched.</summary>
    internal void Recount() => Count = Sections.Sum(s => s.Rows.Count);
}
