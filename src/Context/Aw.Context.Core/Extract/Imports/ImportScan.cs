namespace Aw.Context.Core.Extract.Imports;

/// <summary>The resolved imports of a repo, and how many namespaces were too widely declared to follow.</summary>
/// <param name="Pairs">One pair per importing and imported file, sorted, no self-imports.</param>
/// <param name="SkippedNamespaces">The C# namespaces declared in more files than <see cref="ImportEdges.MaxNamespaceFiles"/>.</param>
internal sealed record ImportScan(IReadOnlyList<ImportPair> Pairs, int SkippedNamespaces);
