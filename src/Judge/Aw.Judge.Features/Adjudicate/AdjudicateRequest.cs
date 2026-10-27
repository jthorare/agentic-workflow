using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.Adjudicate;

/// <summary>The Adjudicate request ('judge adjudicate &lt;question&gt; [--limit 60]').</summary>
/// <param name="Question">The question whose unlabelled items a stronger model labels.</param>
/// <param name="Limit">How many items to try at most (--limit).</param>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider; it must keep claude-cli.</param>
public sealed record AdjudicateRequest(string Question, int Limit = 60, IReadOnlyList<ProviderName>? Providers = null);
