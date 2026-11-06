namespace Aw.Sindri.Core.Reviews;

/// <summary>One of the list's two groups.</summary>
/// <param name="Kind">Which group.</param>
/// <param name="Title">"Waiting on you" or "Yours".</param>
/// <param name="Sections">Project sections first, then repo sections.</param>
public sealed record ReviewGroup(ReviewGroupKind Kind, string Title, IReadOnlyList<ReviewSection> Sections);
