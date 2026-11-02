namespace Aw.Sindri.Core.Setup;

/// <summary>An edit of the live profile that is planned and not yet written.</summary>
/// <param name="Path">The live profile file.</param>
/// <param name="Text">The whole file with the key set.</param>
/// <param name="Changed">Whether <paramref name="Text"/> differs from the file.</param>
public sealed record ProfileKeyEdit(string Path, string Text, bool Changed);
