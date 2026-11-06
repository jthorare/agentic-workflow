namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The rows of every panel kind in one record: a panel fills the field its id names and leaves the others null.</summary>
internal sealed record ReviewPanelData(
    IReadOnlyList<ReviewCheckView>? Checks,
    IReadOnlyList<ReviewClaimView>? Claims,
    ReviewDocView? Doc,
    IReadOnlyList<ReviewTestRowView>? Rows,
    IReadOnlyList<ReviewRunView>? Runs,
    IReadOnlyList<ReviewEvidenceView>? Items,
    IReadOnlyList<ReviewRiskHitView>? Hits,
    IReadOnlyList<ReviewThreadView>? Threads);
