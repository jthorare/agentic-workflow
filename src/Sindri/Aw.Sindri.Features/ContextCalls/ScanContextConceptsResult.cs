namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a scan did. The service reads <paramref name="Version"/> from the job's stored result to know which versions were scanned.</summary>
/// <param name="Version">The map version it looked at, or 0 when there is no map.</param>
/// <param name="OnDraft">True when that version is a draft.</param>
/// <param name="Outcome">'scanned', 'nothing-to-scan', 'model-unavailable', 'no-concepts', 'over-budget' or 'no-map'.</param>
/// <param name="Hints">How many concept hints it stored.</param>
/// <param name="Searches">How many map searches it ran.</param>
/// <param name="Reason">Why it stopped without scanning (the budget's reason), or null.</param>
/// <param name="Tier">'cheap' or 'strong': the model tier the scan picked for the map's size, or null when it made no model call.</param>
/// <param name="Model">The model asked for when it was named (the strong tier), or null when each agent CLI ran its own default.</param>
public sealed record ScanContextConceptsResult(int Version, bool OnDraft, string Outcome, int Hints, int Searches, string? Reason, string? Tier = null, string? Model = null);
