namespace Aw.Meta.Core.Routing;

/// <summary>What a provider's own session file says about the account's usage.</summary>
/// <param name="Windows">The usage windows.</param>
/// <param name="LimitReached">Whether the provider says a limit is reached now.</param>
public sealed record ProviderSignal(IReadOnlyList<ProviderWindow> Windows, bool LimitReached)
{
    /// <summary>The share left in the fullest window; null with no window.</summary>
    public double? RemainingPct => Windows.Count == 0 ? null : 100 - Windows.Max(static w => w.UsedPct);
}
