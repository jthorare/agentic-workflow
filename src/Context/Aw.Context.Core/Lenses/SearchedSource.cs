namespace Aw.Context.Core.Lenses;

/// <summary>One source a lenses answer read, or couldn't (scoping spec C6). Three separate fields, so a reader never parses one for the others.</summary>
/// <param name="Id">The source: map, onyx or code-index.</param>
/// <param name="State">ok, not-configured, unavailable or degraded.</param>
/// <param name="Reason">A recorded-limit word ('index-not-built', 'not-configured', 'not-synced'), or null.</param>
/// <param name="Code">An SND-CONTEXT-* code, or null.</param>
/// <param name="AsOf">How current what was read is (the map version's creation; an intent source's last sync), or null.</param>
public sealed record SearchedSource(Sources.SourceId Id, SearchedState State, string? Reason, string? Code, DateTimeOffset? AsOf)
{
    /// <summary>The sync job that is filling this gap ('not-synced'), or null; the engine reads no jobs, so Sindri sets it from its jobs view.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SearchedSync? Sync { get; init; }

    /// <summary>Both commits and the pending files when the index trails the map along main (reason 'catching-up'); null otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SearchedCatchingUp? CatchingUp { get; init; }

    /// <summary>A sentence about the limit this row records (the deadline that applied and how long the read took), or null; the row stays ok.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }
}
