namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A session state as the page draws it: a glyph and its word. The word is always there, so the glyph is never the only signal.</summary>
/// <param name="Glyph">The mark.</param>
/// <param name="Word">The state's word in sentence case; a state this build doesn't know shows as served.</param>
/// <param name="Tone">Which status brush colours the dot: "working", "waiting" or "quiet". The shape is the same for every state; colour carries it.</param>
internal readonly record struct SessionChip(string Glyph, string Word, string Tone = SessionChip.Quiet)
{
    /// <summary>The tone of a state with no colour of its own (idle, unknown).</summary>
    internal const string Quiet = "quiet";

    /// <summary>The glyph and the word, as the row reads them ("● Working").</summary>
    public string Text => Glyph + " " + Word;
}
