using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Audience;

/// <summary>The audiences one caller may read (spec §4.4, §10): the set a mask over the store is built from.</summary>
/// <param name="Allowed">The audience keys the caller may see.</param>
public sealed record AudienceScope(IReadOnlySet<AudienceKey> Allowed)
{
    /// <summary>A scope that allows nothing.</summary>
    public static AudienceScope None { get; } = new(new HashSet<AudienceKey>());

    /// <summary>
    /// The records the caller may read whatever their audience (spec §4.4 rule (c)): the records a session's scoped map holds because a link ties them to its repo's graph nodes.
    /// Empty for a caller with no such links; a human's Slack channels are audiences, not records.
    /// </summary>
    public IReadOnlySet<RecordKey> Records { get; init; } = new HashSet<RecordKey>();

    /// <summary>Whether a record with this audience is visible: a record with none is visible to no one (default deny). Ignores <see cref="Records"/>; use the two-argument overload for a stored record.</summary>
    /// <param name="audience">The record's audience.</param>
    /// <returns>True when allowed.</returns>
    public bool Allows(AudienceKey? audience) => audience is { } key && Allowed.Contains(key);

    /// <summary>Whether this record is visible: its audience is allowed, or rule (c) lists the record itself.</summary>
    /// <param name="audience">The record's audience.</param>
    /// <param name="record">The record's key.</param>
    /// <returns>True when allowed.</returns>
    public bool Allows(AudienceKey? audience, RecordKey record) => Allows(audience) || Records.Contains(record);
}
