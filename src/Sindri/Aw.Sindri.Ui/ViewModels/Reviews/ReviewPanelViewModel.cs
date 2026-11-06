namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One panel of the review page (review page spec §6.2, §6.3): its title, state, words, rows, and whether it is collapsed.</summary>
internal sealed class ReviewPanelViewModel : ObservableObject
{
    /// <summary>requiredChecks, docMap, testPlan, ci, evidence, risk or conversation.</summary>
    public string Id { get; } = string.Empty;

    /// <summary>The panel's title ("Required checks").</summary>
    public string Title { get; } = string.Empty;

    /// <summary>Loading, ready, empty or error.</summary>
    public ReviewPanelState State { get; } = ReviewPanelState.Loading;

    /// <summary>The words of a loading, empty or error panel; empty when ready.</summary>
    public string Text { get; } = string.Empty;

    /// <summary>The header's summary: the strip's words for the panel.</summary>
    public string Summary { get; } = string.Empty;

    /// <summary>True when collapsed. Setting it keeps the choice per settings name.</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>The required checks, for the requiredChecks panel.</summary>
    public IReadOnlyList<ReviewCheckRowViewModel> Checks { get; } = [];

    /// <summary>The doc map's items, for the docMap panel.</summary>
    public IReadOnlyList<ReviewDocItemViewModel> DocItems { get; } = [];

    /// <summary>The claim line of the docMap panel ("Covers Ph5, REQ-E3"); empty when none.</summary>
    public string ClaimsText { get; } = string.Empty;

    /// <summary>The plain lines of the testPlan, ci, evidence, risk and conversation panels.</summary>
    public IReadOnlyList<ReviewPanelLine> Lines { get; } = [];
}
