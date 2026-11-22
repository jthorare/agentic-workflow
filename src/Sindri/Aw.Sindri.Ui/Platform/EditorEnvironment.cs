namespace Aw.Sindri.Ui.Platform;

/// <summary>The three settings that name the editor a code file opens in, in the order they win: Sindri's own, then the Unix <c>VISUAL</c> and <c>EDITOR</c> (#3715).</summary>
/// <param name="Configured">AW_SINDRI_EDITOR, or null.</param>
/// <param name="Visual">VISUAL, or null.</param>
/// <param name="Editor">EDITOR, or null.</param>
internal sealed record EditorEnvironment(string? Configured, string? Visual, string? Editor)
{
    /// <summary>Reads the three variables of this process.</summary>
    /// <returns>Their values.</returns>
    internal static EditorEnvironment Read() => throw new NotSupportedException();
}
