namespace Aw.Sindri.Ui.Api;

/// <summary>An artifact an 'open' target names.</summary>
/// <param name="Kind">The kind of artifact.</param>
/// <param name="Fact">The fact it evidences.</param>
/// <param name="Path">Its path, when it has one.</param>
/// <param name="Url">Its address, when it has one.</param>
internal sealed record NeedsYouOpenArtifactView(string? Kind = null, string? Fact = null, string? Path = null, string? Url = null);
