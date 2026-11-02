namespace Aw.Judge.Core.Providers;

/// <summary>The provider failed (a bad exit, unparsable output); recorded as a failure with this reason.</summary>
/// <param name="ReasonCode">Why, e.g. 'exit-1' or 'bad-json'.</param>
public sealed record ProviderErrored(string ReasonCode) : ProviderResult(ReasonCode)
{
    /// <summary>
    /// Whether the failure was the harness's usage limit: its CLI reported it on a channel it marks as an error (never text a
    /// model wrote). <see cref="ProviderResult.ReasonCode"/> is the same either way, so recorded decisions don't change; only a
    /// caller that routes between harnesses reads this. No CLI text and no reset time is kept.
    /// </summary>
    public bool UsageLimit { get; init; }

    /// <summary>The id of the pattern that set <see cref="UsageLimit"/> (for tests and logs); null when it isn't a limit.</summary>
    public string? LimitPattern { get; init; }
}
