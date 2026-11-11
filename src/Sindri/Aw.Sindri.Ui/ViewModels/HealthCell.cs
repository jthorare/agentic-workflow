namespace Aw.Sindri.Ui.ViewModels;

/// <summary>One labelled value of the health line (UI spec §4.8): a cell of Home's strip, or a row of the Health page.</summary>
/// <param name="Label">What it is ("Process", "pid · epoch").</param>
/// <param name="Value">The value in words.</param>
/// <param name="Glyph">The value's state glyph, drawn before it (§11.3), or none.</param>
/// <param name="IsPath">True when the value is a path, shown in mono (S3).</param>
/// <param name="IsAttention">True when the value draws in the attention colour (a stalled view).</param>
internal sealed record HealthCell(string Label, string Value, StateGlyph Glyph = StateGlyph.None, bool IsPath = false, bool IsAttention = false)
{
    /// <summary>True when the value has a glyph.</summary>
    public bool HasGlyph => Glyph != StateGlyph.None;
}
