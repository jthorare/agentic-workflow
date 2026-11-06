namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The rows of every panel kind in one record: a panel fills the field its id names and leaves the others null.</summary>
internal sealed record ReviewPanelData(
    IReadOnlyList<ReviewCheckView>? Checks = null,
    IReadOnlyList<ReviewClaimView>? Claims = null,
    ReviewDocView? Doc = null,
    IReadOnlyList<ReviewTestRowView>? Rows = null,
    IReadOnlyList<ReviewRunView>? Runs = null,
    IReadOnlyList<ReviewEvidenceView>? Items = null,
    IReadOnlyList<ReviewRiskHitView>? Hits = null,
    IReadOnlyList<ReviewThreadView>? Threads = null);
