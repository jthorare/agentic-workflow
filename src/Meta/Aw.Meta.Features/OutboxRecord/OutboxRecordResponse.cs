using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>The OutboxRecord response.</summary>
/// <param name="Recorded">Whether the send was recorded; false for any session but the meta.</param>
/// <param name="Closed">The obligations this send closed.</param>
public sealed record OutboxRecordResponse(bool Recorded, IReadOnlyList<Obligation> Closed)
{
    /// <summary>A one-line warning for the sender, set for any session's send (not only the meta's); null when the send is fine.</summary>
    public string? Warning { get; init; }
}
