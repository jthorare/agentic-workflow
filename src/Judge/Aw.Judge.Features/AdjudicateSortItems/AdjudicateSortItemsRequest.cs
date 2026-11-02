using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.AdjudicateSortItems;

/// <summary>The AdjudicateSortItems request ('judge prompt-sort adjudicate [--limit 80]').</summary>
/// <param name="Limit">How many items to try at most (--limit).</param>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider; it must keep claude-cli.</param>
public sealed record AdjudicateSortItemsRequest(int Limit = 80, IReadOnlyList<ProviderName>? Providers = null);
