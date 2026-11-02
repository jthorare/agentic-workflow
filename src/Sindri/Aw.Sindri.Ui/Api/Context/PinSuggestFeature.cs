namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One feature a search found.</summary>
/// <param name="Key">The feature's key.</param>
/// <param name="Label">Its label.</param>
internal sealed record PinSuggestFeature(string Key, string Label);
