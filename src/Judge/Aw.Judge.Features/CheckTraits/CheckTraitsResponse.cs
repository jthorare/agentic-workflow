namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// The CheckTraits response. The command exits 0 when there is nothing to report or the check was skipped, 2 when
/// <paramref name="Findings"/> is not empty (edit and stop: stderr; scan: JSON lines on stdout) and 3 when
/// <paramref name="JevUnavailable"/> (trait-check's own scheme, kept by trait-check.sh).
/// </summary>
/// <param name="Findings">The traits broken, worst first.</param>
/// <param name="JevUnavailable">Jev was excluded, has no key, couldn't be reached or ran out of budget, so nothing was judged.</param>
public sealed record CheckTraitsResponse(IReadOnlyList<TraitFinding> Findings, bool JevUnavailable = false);
