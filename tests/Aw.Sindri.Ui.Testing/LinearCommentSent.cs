namespace Aw.Sindri.Ui.Testing;

/// <summary>A comment the fake was sent.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="ClientId">The app's client id.</param>
/// <param name="Body">The text.</param>
internal sealed record LinearCommentSent(string Key, string ClientId, string Body);
