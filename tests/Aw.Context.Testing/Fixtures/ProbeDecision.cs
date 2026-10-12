namespace Aw.Context.Testing.Fixtures;

/// <summary>A decision a probe's answer must carry.</summary>
/// <param name="Record">The canonical key of the record that holds the quote, e.g. 'linear:issue/&lt;uuid&gt;'.</param>
/// <param name="Quote">Verbatim text from that record.</param>
/// <param name="Status">'current' or 'superseded'.</param>
/// <param name="ConflictsWith">The repo path whose code disagrees with the decision, or null.</param>
public sealed record ProbeDecision(string Record, string Quote, string Status, string? ConflictsWith);
