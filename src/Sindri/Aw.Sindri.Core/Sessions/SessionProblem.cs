namespace Aw.Sindri.Core.Sessions;

/// <summary>A source that failed in a pass (collector spec §4.1): a fixed sentence per code, never an exception message.</summary>
/// <param name="Source">The source: 'processes', 'claude-records', 'fleet-states' or 'collector'.</param>
/// <param name="Code">The stable code, for example 'SND-SESSION-003'.</param>
/// <param name="Text">The sentence a client shows.</param>
public sealed record SessionProblem(string Source, string Code, string Text)
{
    /// <summary>Why the collector can't run, set on the SND-SESSION-002 problem only; not served (the code and text say it to clients).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public UnavailableReason? Reason { get; init; }

    /// <summary>True on the collector's row for a pass that overran its budget: listed, but no health problem (slow is not broken); not served.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsOverrun { get; init; }
}
