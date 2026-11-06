using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One project's rows, or one repo's (review page spec §6.1). A refresh updates it in place.</summary>
internal sealed class ReviewSectionViewModel
{
    internal ReviewSectionViewModel(ReviewSectionView section)
    {
        Name = section.Name;
        Kind = string.Equals(section.By, "project", StringComparison.Ordinal) ? Labels.ReviewsProjectKind : null;
    }

    /// <summary>The project's name, or the repo's owner/name (drawn mono).</summary>
    public string Name { get; }

    /// <summary>"project" beside a project's name; null for a repo section.</summary>
    public string? Kind { get; }

    /// <summary>True for a project section.</summary>
    public bool IsProject => Kind is not null;

    /// <summary>Its rows.</summary>
    public ObservableCollection<ReviewRowViewModel> Rows { get; } = [];
}
