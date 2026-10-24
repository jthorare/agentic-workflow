using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>The CheckTraits request ('judge trait-check --edit|--stop|--scan …').</summary>
/// <param name="Mode">What to judge.</param>
/// <param name="Files">The edited files (<see cref="TraitCheckMode.Edit"/>), as the hook names them.</param>
/// <param name="BaseRef">The ref a <see cref="TraitCheckMode.Stop"/> diff starts from; null for 'origin/main'.</param>
/// <param name="Paths">The paths a <see cref="TraitCheckMode.Scan"/> limits itself to; empty for the whole repo.</param>
/// <param name="Traits">The traits to check; empty for every trait of the repo.</param>
/// <param name="Budget">The time budget (--budget-ms), or null for the mode's default.</param>
/// <param name="MinProbability">The lowest probability a finding is reported at (--min-p), or null for the default.</param>
/// <param name="Concurrency">How many Jev requests run at once (--concurrency), or null for the default.</param>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider (with Jev excluded nothing is judged and the check is Jev-unavailable, exit 3).</param>
public sealed record CheckTraitsRequest(
    TraitCheckMode Mode,
    IReadOnlyList<string> Files,
    string? BaseRef = null,
    IReadOnlyList<string>? Paths = null,
    IReadOnlyList<string>? Traits = null,
    TimeSpan? Budget = null,
    double? MinProbability = null,
    int? Concurrency = null,
    IReadOnlyList<ProviderName>? Providers = null);
