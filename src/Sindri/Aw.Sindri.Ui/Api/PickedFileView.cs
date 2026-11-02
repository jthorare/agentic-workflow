namespace Aw.Sindri.Ui.Api;

/// <summary>A file Jev saw.</summary>
/// <param name="Path">The repo path.</param>
/// <param name="Chars">How many characters of it were shown.</param>
internal sealed record PickedFileView(string Path, int Chars);
