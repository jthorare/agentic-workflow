namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The cached page's note: when it was cached and the words.</summary>
internal sealed record ReviewOfflineView(DateTimeOffset CachedAt, string Text);
