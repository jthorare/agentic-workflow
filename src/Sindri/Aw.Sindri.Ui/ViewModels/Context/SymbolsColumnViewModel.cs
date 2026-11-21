using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map's symbols column (S-9): one cell per file, and the caption the lens toggle and the footer read.</summary>
internal sealed class SymbolsColumnViewModel
{
    private const int NamesShown = 2;

    private readonly Dictionary<string, FileSymbolsEntry> _files;

    /// <summary>The column over what the file map served.</summary>
    /// <param name="symbols">The served symbols object.</param>
    internal SymbolsColumnViewModel(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        (Caption, Dimmed) = SymbolsCaption.Of(symbols);
        if (string.Equals(symbols.State, "ok", StringComparison.Ordinal) && symbols.Files is { Count: 0 })
        {
            Caption = Labels.ContextSymbolsNoneIndexed;   // an index that answers but knows none of these files (e.g. .ts without graphify): say so, not just the commit
        }

        Unavailable = string.Equals(symbols.State, "unavailable", StringComparison.Ordinal);
        _files = (symbols.Files ?? []).ToDictionary(static f => f.Path, StringComparer.Ordinal);
    }

    /// <summary>"from 7c3e1a2", "from abcdef0, not 1234567", "index not built", ...</summary>
    internal string Caption { get; }

    /// <summary>Whether every cell is drawn dimmed (stale or as-of mismatch).</summary>
    internal bool Dimmed { get; }

    /// <summary>Whether the index cannot answer: every cell says the caption instead of names.</summary>
    internal bool Unavailable { get; }

    /// <summary>The cell of one file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>"setOrientation · resize · +3"; the caption when the index cannot answer; a dash for a file the index does not know.</returns>
    internal string Cell(string path)
    {
        if (Unavailable)
        {
            return Caption;
        }

        if (!_files.TryGetValue(path, out var file) || file.Symbols.Count == 0)
        {
            return Labels.ContextSymbolsNoRow;
        }

        return Labels.ContextSymbolsCell([.. file.Symbols.Take(NamesShown).Select(static s => s.Name)], Math.Max(0, file.Symbols.Count - NamesShown));
    }
}
