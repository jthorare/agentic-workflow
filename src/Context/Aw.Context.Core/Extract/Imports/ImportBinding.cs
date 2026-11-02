namespace Aw.Context.Core.Extract.Imports;

/// <summary>One name an import statement binds in a file.</summary>
/// <param name="Local">The name used in the importing file.</param>
/// <param name="Imported">The name the other module exports it as ('default' for a default import, empty for a namespace).</param>
/// <param name="Specifier">The module specifier as written.</param>
/// <param name="Kind">How it was imported.</param>
internal sealed record ImportBinding(string Local, string Imported, string Specifier, ImportKind Kind);
