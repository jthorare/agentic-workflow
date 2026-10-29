namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Where concept synthesis stands, as 'sindri index status' shows it: counted live when the status is asked for, nothing kept (#1597). A failed read carries only <paramref name="Code"/>.</summary>
/// <param name="Enabled">Whether the decision pass synthesizes concepts (context.conceptSynthesis and labeling on).</param>
/// <param name="Concepts">How many concepts are stored, or null when the read failed.</param>
/// <param name="MultiSource">How many of them group records of two or more sources.</param>
/// <param name="Queued">How many wait for the model to name them.</param>
/// <param name="Covered">How many distinct records the named concepts group.</param>
/// <param name="Embedded">How many live records have a chunk vector.</param>
/// <param name="Pending">How many live records have no vector yet.</param>
/// <param name="Watermark">The embedding.seq high-water the last pass reached.</param>
/// <param name="Coverage">Covered over Embedded, 0 to 1 (0 when Embedded is 0 or unknown).</param>
/// <param name="Code">The error code of a failed read, or null.</param>
/// <param name="LastRun">When the index-decisions run last ended, or null when none was recorded.</param>
/// <param name="LastCode">The code that run ended with; null after a success.</param>
public sealed record IndexConceptsView(bool Enabled, int Concepts = 0, int MultiSource = 0, int Queued = 0, int Covered = 0, int? Embedded = null, int? Pending = null, double Coverage = 0, long Watermark = 0, string? Code = null, DateTimeOffset? LastRun = null, string? LastCode = null);
