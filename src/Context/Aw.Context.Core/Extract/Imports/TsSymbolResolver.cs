namespace Aw.Context.Core.Extract.Imports;

/// <summary>
/// Resolves a name used in one TS or JS file to the file that declares it: through the file's imports (named, renamed, namespace), the aliases and workspace
/// packages <see cref="TsResolver"/> knows, and re-exports ('export { a } from', 'export * from', barrels). Pure text in, paths out; it knows nothing of what a
/// symbol means, so a caller that wants "which file defines the function this file calls" uses it as it is.
/// </summary>
internal sealed class TsSymbolResolver
{
    private const int MaxDepth = 16;

    private readonly TsResolver _modules;
    private readonly Dictionary<string, string> _text;
    private readonly Dictionary<string, IReadOnlyList<ImportBinding>> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModuleExports> _exports = new(StringComparer.Ordinal);

    internal TsSymbolResolver(IReadOnlyList<SourceFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _modules = new TsResolver(files);
        _text = files.GroupBy(f => f.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);
    }

    /// <summary>The declaration of a name <paramref name="from"/> imports under the local name <paramref name="local"/>.</summary>
    /// <param name="from">The importing file's repo-relative path.</param>
    /// <param name="local">The name as the file uses it.</param>
    /// <returns>Where it is declared, or null when the file does not import it, the module is not in the repo, or the name is not found.</returns>
    internal ResolvedSymbol? Resolve(string from, string local)
    {
        var binding = BindingsOf(from).FirstOrDefault(b => string.Equals(b.Local, local, StringComparison.Ordinal) && b.Kind != ImportKind.Namespace);
        return binding is not null && _modules.Resolve(from, binding.Specifier) is { } file ? Exported(file, binding.Imported, [], 0) : null;
    }

    /// <summary>The declaration of 'ns.member' where 'ns' is a namespace import of <paramref name="from"/>.</summary>
    /// <param name="from">The importing file's repo-relative path.</param>
    /// <param name="ns">The namespace name as the file uses it.</param>
    /// <param name="member">The member name.</param>
    /// <returns>Where it is declared, or null.</returns>
    internal ResolvedSymbol? Member(string from, string ns, string member)
    {
        var binding = BindingsOf(from).FirstOrDefault(b => string.Equals(b.Local, ns, StringComparison.Ordinal) && b.Kind == ImportKind.Namespace);
        return binding is not null && _modules.Resolve(from, binding.Specifier) is { } file ? Exported(file, member, [], 0) : null;
    }

    private ResolvedSymbol? Exported(string file, string name, HashSet<(string, string)> seen, int depth)
    {
        if (depth >= MaxDepth || !seen.Add((file, name)))
        {
            return null;   // a re-export cycle, or a chain longer than any real barrel
        }

        var exports = ExportsOf(file);
        if (exports.Declared.Contains(name))
        {
            return new ResolvedSymbol(file, name);
        }

        if (exports.Aliases.TryGetValue(name, out var local))
        {
            var imported = BindingsOf(file).FirstOrDefault(b => string.Equals(b.Local, local, StringComparison.Ordinal) && b.Kind != ImportKind.Namespace);
            return imported is not null && _modules.Resolve(file, imported.Specifier) is { } target ? Exported(target, imported.Imported, seen, depth + 1) : new ResolvedSymbol(file, local);
        }

        if (exports.ReExports.TryGetValue(name, out var from))
        {
            return _modules.Resolve(file, from.Specifier) is { } target ? Exported(target, from.Name, seen, depth + 1) : null;
        }

        return exports.Stars.Select(s => _modules.Resolve(file, s)).OfType<string>().Select(target => Exported(target, name, seen, depth + 1)).FirstOrDefault(r => r is not null);
    }

    private IReadOnlyList<ImportBinding> BindingsOf(string file)
    {
        if (!_bindings.TryGetValue(file, out var bindings))
        {
            bindings = _text.TryGetValue(file, out var text) ? TsImportBindings.Read(text) : [];
            _bindings[file] = bindings;
        }

        return bindings;
    }

    private ModuleExports ExportsOf(string file)
    {
        if (!_exports.TryGetValue(file, out var exports))
        {
            exports = _text.TryGetValue(file, out var text) ? TsExports.Read(text) : ModuleExports.Empty;
            _exports[file] = exports;
        }

        return exports;
    }
}
