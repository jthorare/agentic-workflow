namespace Aw.Sindri.Core.Line;

/// <summary>One file of a packet an assembler wrote: its path relative to the packet directory and its text.</summary>
/// <param name="Path">The path, for example 'index.json'.</param>
/// <param name="Content">The text, ending with a newline.</param>
public sealed record LineFile(string Path, string Content);
