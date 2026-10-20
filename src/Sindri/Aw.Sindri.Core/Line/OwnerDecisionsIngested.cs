namespace Aw.Sindri.Core.Line;

/// <summary>What the ingest did.</summary>
/// <param name="Stored">How many decision records it stored.</param>
/// <param name="Skipped">Why it stored none of a non-empty set (the ingest does not exist yet), or null.</param>
public sealed record OwnerDecisionsIngested(int Stored, string? Skipped);
