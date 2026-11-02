namespace Aw.Context.Core.Extract.Imports;

/// <summary>The imports of the repo's TS and JS files that resolve to another file of the repo.</summary>
internal static class TsImports
{
    private static readonly string[] _sourceExtensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];

    internal static IReadOnlyList<ImportPair> Find(IReadOnlyList<SourceFile> files)
    {
        var resolver = new TsResolver(files);
        return [.. files.Where(IsSource).SelectMany(file => Imports(resolver, file))];
    }

    private static bool IsSource(SourceFile file) => _sourceExtensions.Any(e => file.Path.EndsWith(e, StringComparison.Ordinal));

    private static IEnumerable<ImportPair> Imports(TsResolver resolver, SourceFile file) =>
        TsSpecifiers.Read(file.Text).Select(s => resolver.Resolve(file.Path, s)).OfType<string>().Select(to => new ImportPair(file.Path, to));
}
