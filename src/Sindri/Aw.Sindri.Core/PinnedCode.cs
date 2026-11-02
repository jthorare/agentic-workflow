namespace Aw.Sindri.Core;

/// <summary>The code files of one pin.</summary>
/// <param name="Pin">The pin's id.</param>
/// <param name="MapTag">The map's tag ('v41').</param>
/// <param name="MapCommit">The commit the map was built at, or null.</param>
/// <param name="Files">The files.</param>
public sealed record PinnedCode(string Pin, string MapTag, string? MapCommit, IReadOnlyList<PinnedFile> Files);
