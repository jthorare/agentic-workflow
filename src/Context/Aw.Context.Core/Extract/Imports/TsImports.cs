namespace Aw.Context.Core.Extract.Imports;

/// <summary>
/// The imports of the repo's TS and JS files that resolve to another file of the repo: the file each specifier names, and, for each name the file imports
/// (named or default), the file that declares it when a barrel stands between them, so a user of a service imported through 'export * as Ns' or 'export *'
/// also depends on the service's own file.
/// </summary>
internal static class TsImports
{
    private static readonly string[] _sourceExtensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];

    internal static IReadOnlyList<ImportPair> Find(IReadOnlyList<SourceFile> files)
    {
        var resolver = new TsSymbolResolver(files);
        return [.. files.Where(IsSource).SelectMany(file => Imports(resolver, file))];
    }

    private static bool IsSource(SourceFile file) => _sourceExtensions.Any(e => file.Path.EndsWith(e, StringComparison.Ordinal));

    private static IEnumerable<ImportPair> Imports(TsSymbolResolver resolver, SourceFile file)
    {
        var modules = TsSpecifiers.Read(file.Text).Select(s => resolver.Module(file.Path, s));
        var declaring = TsImportBindings.Read(file.Text).Where(b => b.Kind != ImportKind.Namespace).Select(b => resolver.Resolve(file.Path, b.Local)?.File);
        return modules.Concat(declaring).OfType<string>().Distinct(StringComparer.Ordinal).Select(to => new ImportPair(file.Path, to));
    }
}
