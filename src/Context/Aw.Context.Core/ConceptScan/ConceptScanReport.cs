using Aw.Context.Core.Models;

namespace Aw.Context.Core.ConceptScan;

/// <summary>What one scan did.</summary>
/// <param name="Version">The map version it scanned: the latest draft, else the current version.</param>
/// <param name="OnDraft">True when <paramref name="Version"/> is a draft.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Hints">How many concept hints it stored.</param>
/// <param name="Searches">How many map searches ran between the two model passes.</param>
/// <param name="Tier">The model tier the scan picked for its size, or null when it never reached a model call.</param>
/// <param name="Model">The model the port asked for, or null when it left the choice to the agent CLI or no call was made.</param>
/// <param name="CutOff">True when a model reply ended mid-JSON and the scan kept the concepts that were whole.</param>
public sealed record ConceptScanReport(int Version, bool OnDraft, ConceptScanOutcome Outcome, int Hints, int Searches, ModelTier? Tier = null, string? Model = null, bool CutOff = false);
