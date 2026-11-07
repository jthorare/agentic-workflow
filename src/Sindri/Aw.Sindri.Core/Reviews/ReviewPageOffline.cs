namespace Aw.Sindri.Core.Reviews;

/// <summary>The page is the cached one.</summary>
/// <param name="CachedAt">When it was cached.</param>
/// <param name="Text">The words: as of 23:45, the PR may have moved.</param>
public sealed record ReviewPageOffline(
    DateTimeOffset CachedAt,
    string Text);
