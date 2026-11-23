namespace Aw.Context.Core.Extract.Imports;

/// <summary>A symbol found where it is declared.</summary>
/// <param name="File">The repo-relative path of the declaring file.</param>
/// <param name="Name">The name it is declared and exported under there, or <see cref="TsSymbolResolver.WholeModule"/> when the symbol is the module itself ('export * as Ns', 'import * as Ns').</param>
internal sealed record ResolvedSymbol(string File, string Name);
