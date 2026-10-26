namespace Aw.Context.Core.ConceptScan;

/// <summary>What one scan did.</summary>
/// <param name="Version">The map version it scanned: the latest draft, else the current version.</param>
/// <param name="OnDraft">True when <paramref name="Version"/> is a draft.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Hints">How many concept hints it stored.</param>
/// <param name="Searches">How many map searches ran between the two model passes.</param>
public sealed record ConceptScanReport(int Version, bool OnDraft, ConceptScanOutcome Outcome, int Hints, int Searches);
