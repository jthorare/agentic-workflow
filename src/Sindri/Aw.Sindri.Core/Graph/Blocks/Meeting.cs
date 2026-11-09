namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One meeting a 'meetings' node reads. The notes and transcript are what people said: untrusted wherever they reach a prompt.</summary>
/// <param name="Title">The meeting's title.</param>
/// <param name="Start">When it started.</param>
/// <param name="End">When it ended.</param>
/// <param name="Attendees">The people in it, as the app names them.</param>
/// <param name="Notes">The notes, empty when the app has none.</param>
/// <param name="Transcript">The full text, or null when the app has none or the node did not ask for it.</param>
/// <param name="Link">The meeting's page in the app.</param>
/// <param name="Source">The preset that made it, such as 'granola'.</param>
public sealed record Meeting(string Title, DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<string> Attendees, string Notes, string? Transcript, string Link, string Source);
