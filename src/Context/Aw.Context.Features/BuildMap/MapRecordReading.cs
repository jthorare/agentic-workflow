using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>What the store gave the build.</summary>
/// <param name="Map">The record nodes and their edges.</param>
/// <param name="Stamp">The store's record stamp as the build read it.</param>
/// <param name="Notes">Every record line of the build report, numbers only.</param>
internal sealed record MapRecordReading(ExtractedMap Map, long Stamp, IReadOnlyList<string> Notes)
{
    /// <summary>The lines of <see cref="Notes"/> that say something is missing or stale: they alone are degraded; the rest are report notes.</summary>
    public IReadOnlyList<string> Degraded { get; init; } = [];

    /// <summary>The records over <see cref="MapRecordSources.TouchCap"/> files: no file edges in <see cref="Map"/>, they roll up to features once the build has assigned the homes.</summary>
    public IReadOnlyList<RolledRecord> Overflow { get; init; } = [];
}
