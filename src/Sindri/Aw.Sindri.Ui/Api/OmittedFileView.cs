namespace Aw.Sindri.Ui.Api;

/// <summary>A file left out of the pick.</summary>
/// <param name="Path">The repo path.</param>
/// <param name="Chars">Its size in characters.</param>
/// <param name="Why">'cap', 'denied' or 'secret'.</param>
internal sealed record OmittedFileView(string Path, int Chars, string Why);
