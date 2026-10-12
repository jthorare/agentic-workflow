namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>
/// Validate a recipe in-process (spec §6.1, W27): either a file by <see cref="Path"/>, or <see cref="Text"/> with the
/// <see cref="File"/> name findings show. Child recipes resolve from the file's directory, or from the registered
/// recipes when served.
/// </summary>
/// <param name="Path">The recipe file to read; null when <paramref name="Text"/> is given.</param>
/// <param name="Text">The recipe's contents; null when <paramref name="Path"/> is given.</param>
/// <param name="File">The name findings show for <paramref name="Text"/>; null with <paramref name="Path"/>, whose file name findings show (spec P1 signal: 'at RCP-REACH-001.yaml:14:3').</param>
public sealed record ValidateRecipeRequest(string? Path, string? Text, string? File)
{
    /// <summary>Validate the file at <paramref name="path"/>.</summary>
    /// <param name="path">The recipe file, as the user gave it.</param>
    /// <returns>The request.</returns>
    public static ValidateRecipeRequest FromPath(string path) => new(path, Text: null, File: null);

    /// <summary>Validate <paramref name="text"/>, shown as <paramref name="file"/>.</summary>
    /// <param name="text">The recipe's contents.</param>
    /// <param name="file">The file name findings show.</param>
    /// <returns>The request.</returns>
    public static ValidateRecipeRequest FromText(string text, string file) => new(Path: null, text, file);
}
