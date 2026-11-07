namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One review thread.</summary>
internal sealed record ReviewThreadView(string Id, string Path, int? Line, bool Resolved, bool Outdated, IReadOnlyList<ReviewCommentView> Comments);
