using Aw.Context.Core.Lenses;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Core.Code;

/// <summary>
/// The 'symbols' object of the file map and of the lenses' code band (Phase 9): what the code index says about some files as of the map version's commit,
/// and their file-level 'implements' refined to symbols. A read that fails or an index that cannot answer is a column that says so and leaves the links
/// at file level; it is never an error of the route and never a failed map build.
/// </summary>
public static class SymbolColumn
{
    /// <summary>Reads the column.</summary>
    /// <param name="source">The code index port.</param>
    /// <param name="reading">What to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The column.</returns>
    public static async ValueTask<LensSymbols> Read(ICodeSymbolSource source, SymbolColumnReading reading, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reading);
        var (repo, version) = (reading.Repo, reading.Version);
        var asOf = repo.Name == version.Repo && CodeIndexReading.IsCommit(version.Commit) ? version.Commit : null;
        var head = await source.Heads(repo.Name, asOf, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return Down(head.Error.Code);
        }

        var asked = reading.Paths.Take(CodeRules.MaxSymbolFiles).ToList();
        if (asked.Count == 0 || !CodeIndexReading.Readable(head.Value))
        {
            return CodeIndexReading.FileMap(head.Value, []);
        }

        var files = await source.FileSymbols(repo.Name, asked, asOf, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return Down(files.Error.Code);
        }

        var column = CodeIndexReading.FileMap(files.Value.Head, files.Value.Rows);
        return column.Files is null ? column : column with { Implements = SymbolLinks.For(reading.Links, files.Value.Rows) };
    }

    private static LensSymbols Down(string code) => LensSymbols.Unavailable with { Reason = "index unavailable", Code = code };
}
