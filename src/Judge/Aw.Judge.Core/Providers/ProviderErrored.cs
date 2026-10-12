namespace Aw.Judge.Core.Providers;

/// <summary>The provider failed (a bad exit, unparsable output); recorded as a failure with this reason.</summary>
/// <param name="ReasonCode">Why, e.g. 'exit-1' or 'bad-json'.</param>
public sealed record ProviderErrored(string ReasonCode) : ProviderResult(ReasonCode);
