using System.Diagnostics.CodeAnalysis;
namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>Maps a row's kind and state class to the glyph it is drawn with.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal static class RowGlyphs
{
    /// <summary>Picks the glyph for a row.</summary>
    /// <param name="kind">The row's kind: pr, issue, run, thread, session, item, decision, note.</param>
    /// <param name="state">The row's state class.</param>
    /// <param name="stateKey">The API's state key, for a glyph the class alone cannot pick (draft, in review, CI running); null when not needed.</param>
    /// <returns>The glyph.</returns>
    public static RowGlyph For(string kind, RowStateClass state, string? stateKey = null) => throw new NotSupportedException("RowGlyphs.For is not built yet (#2978).");
}
