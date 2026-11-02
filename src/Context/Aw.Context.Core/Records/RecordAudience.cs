using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>What a record read may return for a caller: the audiences it may see and the records it may read whatever their audience (spec §4.4 rule (c)). The store-side shape of the audience scope, so Records never depends on Audience.</summary>
/// <param name="Allowed">The audience keys the caller may see.</param>
/// <param name="Records">The records readable whatever their audience.</param>
public sealed record RecordAudience(IReadOnlySet<AudienceKey> Allowed, IReadOnlySet<RecordKey> Records)
{
    /// <summary>Whether a record with this audience and key is visible: its audience is allowed, or the record itself is listed. A record with no audience is visible to no one (default deny).</summary>
    /// <param name="audience">The record's audience.</param>
    /// <param name="record">The record's key.</param>
    /// <returns>True when visible.</returns>
    public bool Allows(AudienceKey? audience, RecordKey record) => (audience is { } key && Allowed.Contains(key)) || Records.Contains(record);
}
