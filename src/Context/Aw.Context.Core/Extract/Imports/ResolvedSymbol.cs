namespace Aw.Context.Core.Extract.Imports;

/// <summary>A symbol found where it is declared.</summary>
/// <param name="File">The repo-relative path of the declaring file.</param>
/// <param name="Name">The name it is declared and exported under there.</param>
internal sealed record ResolvedSymbol(string File, string Name);
