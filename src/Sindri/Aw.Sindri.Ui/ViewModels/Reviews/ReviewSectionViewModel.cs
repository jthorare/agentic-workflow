namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One project's rows, or one repo's (review page spec §6.1).</summary>
internal sealed class ReviewSectionViewModel
{
    /// <summary>The project's name, or the repo's owner/name (drawn mono).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>"project" beside a project's name; null for a repo section.</summary>
    public string? Kind { get; private set; }

    /// <summary>Its rows.</summary>
    public IReadOnlyList<ReviewRowViewModel> Rows { get; private set; } = [];
}
