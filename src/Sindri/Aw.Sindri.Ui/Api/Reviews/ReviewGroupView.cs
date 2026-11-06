namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One of the list's two groups (review page spec §6.1).</summary>
/// <param name="Kind">"waiting" or "yours".</param>
/// <param name="Title">"Waiting on you" or "Yours".</param>
/// <param name="Sections">The rows by project, then by repo name.</param>
internal sealed record ReviewGroupView(string Kind, string Title, IReadOnlyList<ReviewSectionView> Sections);
