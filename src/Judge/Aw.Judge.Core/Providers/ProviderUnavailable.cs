namespace Aw.Judge.Core.Providers;

/// <summary>The provider couldn't answer: not installed, no key, or 'timeout' (which the engine counts as a failure).</summary>
/// <param name="ReasonCode">Why, e.g. 'no-api-key' or 'timeout'.</param>
public sealed record ProviderUnavailable(string ReasonCode) : ProviderResult(ReasonCode)
{
    /// <summary>The reason code a provider returns when it ran past the question's time budget.</summary>
    public const string Timeout = "timeout";
}
