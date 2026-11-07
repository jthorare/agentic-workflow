namespace Aw.Sindri.Core.Reviews;

/// <summary>One risky path touched.</summary>
/// <param name="Path">The file.</param>
/// <param name="Why">Why it is risky.</param>
/// <param name="Source">settings, body or handoff.</param>
public sealed record ReviewPageRiskHit(
    string Path,
    string Why,
    string Source);
