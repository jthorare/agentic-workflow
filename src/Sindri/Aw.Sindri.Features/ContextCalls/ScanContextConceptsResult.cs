namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a scan did. The service reads <paramref name="Version"/> from the job's stored result to know which versions were scanned.</summary>
/// <param name="Version">The map version it looked at, or 0 when there is no map.</param>
/// <param name="OnDraft">True when that version is a draft.</param>
/// <param name="Outcome">'scanned', 'nothing-to-scan', 'model-unavailable', 'no-concepts', 'over-budget' or 'no-map'.</param>
/// <param name="Hints">How many concept hints it stored.</param>
/// <param name="Searches">How many map searches it ran.</param>
/// <param name="Reason">Why it stopped without scanning (the budget's reason), or null.</param>
public sealed record ScanContextConceptsResult(int Version, bool OnDraft, string Outcome, int Hints, int Searches, string? Reason);
