using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Core.Extract;

/// <summary>Reads the imports of a repo's files and resolves each to a file of the same repo. It sits in Extract and hands the readers in Extract.Imports plain <see cref="SourceFile"/>s, so that namespace never points back here (ARCH-CYCLE-001).</summary>
internal static class ImportEdges
{
    /// <summary>A C# namespace declared in more files than this makes no edges.</summary>
    internal const int MaxNamespaceFiles = 50;

    /// <summary>Finds the resolved imports.</summary>
    /// <param name="files">The repo's files.</param>
    /// <returns>The pairs and the skipped-namespace count.</returns>
    internal static ImportScan Find(IReadOnlyList<RepoFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var own = files.Where(f => !RepoPath.InDependencyFolder(f.Path)).Select(static f => new SourceFile(f.Path, f.Text)).ToList();
        var csharp = CsImports.Find(own, MaxNamespaceFiles);
        var pairs = TsImports.Find(own).Concat(csharp.Pairs)
            .Where(p => !string.Equals(p.From, p.To, StringComparison.Ordinal))
            .Distinct()
            .OrderBy(p => p.From, StringComparer.Ordinal).ThenBy(p => p.To, StringComparer.Ordinal)
            .ToList();
        return new ImportScan(pairs, csharp.SkippedNamespaces);
    }
}
