namespace Aw.Sindri.Core.Setup;

/// <summary>An edit of the live profile that is planned and not yet written.</summary>
/// <param name="Path">The live profile file.</param>
/// <param name="Text">The whole file with the key set.</param>
/// <param name="Changed">Whether <paramref name="Text"/> differs from the file.</param>
/// <param name="Before">The exact file text the plan read, or null when the planner did not keep it. A save refuses when the file no longer holds it.</param>
public sealed record ProfileKeyEdit(string Path, string Text, bool Changed, string? Before = null);
