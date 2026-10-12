namespace Aw.Judge.Core.Providers;

/// <summary>A provider's answer: <see cref="ProviderDecided"/>, <see cref="ProviderUnavailable"/> or <see cref="ProviderErrored"/>.</summary>
/// <param name="ReasonCode">Why, e.g. 'model', 'no-api-key', 'timeout' or 'exit-1'.</param>
public abstract record ProviderResult(string ReasonCode);
