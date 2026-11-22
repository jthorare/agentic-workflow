namespace Aw.Sindri.Ui.Platform;

/// <summary>Picks the editor command (#3715): AW_SINDRI_EDITOR, then VISUAL, then EDITOR; the first one that is not blank, split on whitespace into an argv list. No shell reads it.</summary>
internal static class EditorCommand
{
    /// <summary>The command's argv without the file.</summary>
    /// <param name="environment">The three settings.</param>
    /// <returns>The program and its arguments, or null when none is set.</returns>
    internal static IReadOnlyList<string>? Resolve(EditorEnvironment environment) 
    {
        _ = environment;
        throw new NotSupportedException();
    }
}
