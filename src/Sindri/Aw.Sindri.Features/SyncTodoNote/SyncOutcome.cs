using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>What one sync pass did, for the status and the log line.</summary>
/// <param name="State">Where the sync stands after it.</param>
/// <param name="Error">Why it failed, or null when it didn't.</param>
internal sealed record SyncOutcome(TodoNoteState State, string? Error)
{
    /// <summary>Bare lines left as text (one-way mode, or past the adoption cap).</summary>
    public int NotAdopted { get; init; }

    /// <summary>Events appended.</summary>
    public int Events { get; init; }

    /// <summary>Lines written to the note.</summary>
    public int Written { get; init; }

    /// <summary>Conflicts the merge found.</summary>
    public int Conflicts { get; init; }

    /// <summary>Task lines read.</summary>
    public int LinesRead { get; init; }

    /// <summary>Lines adopted from the note.</summary>
    public int Adopted { get; init; }

    /// <summary>The sha256 of the note as the pass read it, or empty.</summary>
    public string NoteHash { get; init; } = string.Empty;

    /// <summary>The one JSON log line of a pass (todo spec §4.5 Failures): the note's path inside its vault, never a todo's text.</summary>
    /// <param name="place">The note.</param>
    /// <param name="milliseconds">How long the pass took.</param>
    /// <returns>The JSON.</returns>
    public string Line(NotePlace place, long milliseconds)
    {
        ArgumentNullException.ThrowIfNull(place);
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("note", place.Note);
            json.WriteString("noteHash", NoteHash);
            json.WriteNumber("linesRead", LinesRead);
            json.WriteNumber("eventsWritten", Events);
            json.WriteNumber("linesWritten", Written);
            json.WriteNumber("conflicts", Conflicts);
            json.WriteNumber("durationMs", milliseconds);
            json.WriteString("outcome", Error is null ? State.ToString() : "Failed");
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
