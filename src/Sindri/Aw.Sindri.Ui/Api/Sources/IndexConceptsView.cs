namespace Aw.Sindri.Ui.Api.Sources;

/// <summary>The concepts part of GET /sindri/index/status (#1597): counted live from the store at request time, kept nowhere.</summary>
/// <param name="Enabled">False when concept synthesis or labeling is off.</param>
/// <param name="Concepts">Stored concepts.</param>
/// <param name="MultiSource">Concepts citing two or more sources.</param>
/// <param name="Queued">Groups waiting for the model to name them.</param>
/// <param name="Covered">Distinct records inside a named concept.</param>
/// <param name="Embedded">Live records with a vector, or null when unknown.</param>
/// <param name="Pending">Live records with no vector yet, or null when unknown.</param>
/// <param name="Coverage">Covered over embedded, 0 to 1; 0 while nothing is embedded.</param>
/// <param name="Watermark">The embedding sequence the last pass reached.</param>
/// <param name="Code">Set when the concepts read failed; every count is then 0.</param>
internal sealed record IndexConceptsView(
    bool Enabled,
    int Concepts = 0,
    int MultiSource = 0,
    int Queued = 0,
    int Covered = 0,
    int? Embedded = null,
    int? Pending = null,
    double Coverage = 0,
    long Watermark = 0,
    string? Code = null);
