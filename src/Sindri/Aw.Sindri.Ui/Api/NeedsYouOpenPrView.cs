namespace Aw.Sindri.Ui.Api;

/// <summary>A pull request an 'open' target names.</summary>
/// <param name="Url">Its address.</param>
/// <param name="Number">Its number.</param>
internal sealed record NeedsYouOpenPrView(string? Url = null, int? Number = null);
