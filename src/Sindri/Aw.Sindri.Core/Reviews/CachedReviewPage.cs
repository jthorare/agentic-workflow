namespace Aw.Sindri.Core.Reviews;

/// <summary>A cached page and when it was cached.</summary>
/// <param name="Page">The page as it was built.</param>
/// <param name="CachedAt">When it was written.</param>
public sealed record CachedReviewPage(ReviewPageModel Page, DateTimeOffset CachedAt);
