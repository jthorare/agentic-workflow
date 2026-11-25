using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map's symbols column (S-9): one cell per file, and the caption the lens toggle and the footer read.</summary>
internal sealed class SymbolsColumnViewModel
{
    private const int NamesShown = 2;

    private readonly Dictionary<string, FileSymbolsEntry> _files;
    private readonly string? _graphWhy;   // the graph reason sentence, when the graph is off or did not build
    private readonly bool _reasonIsSentence;   // the empty reason is a graph reason: whole sentences, not a "symbols · ..." fragment (#3862)

    /// <summary>The column over what the file map served.</summary>
    /// <param name="symbols">The served symbols object.</param>
    internal SymbolsColumnViewModel(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        (Caption, Dimmed) = SymbolsCaption.Of(symbols);
        if (symbols.AsOfMismatch && symbols.Commit is { } commit && symbols.AsOfCommit is not null && !string.Equals(symbols.State, "unavailable", StringComparison.Ordinal))
        {
            // "from abcdef0, not 1234567" does not fit the heading at 1000 wide (#3996): the heading keeps "from abcdef0", the note line says the rest.
            AsOfNote = Caption;
            Caption = Labels.ContextSymbolsFrom(commit);
        }

        var graphWhy = GraphWhy(symbols.Reason);
        _graphWhy = graphWhy;
        if (string.Equals(symbols.State, "ok", StringComparison.Ordinal) && symbols.Files is { Count: 0 })
        {
            // An index that answers with no rows: say why, on the note line under the table, where a sentence has room (#3996). Graph off or down = only C# files get names (the real app's .ts names come from the graph layer); graph on = the files have no functions.
            EmptyReason = graphWhy ?? Labels.ContextSymbolsNoneIndexed;
            _reasonIsSentence = graphWhy is not null;
        }

        NoRowTip = graphWhy;

        Unavailable = string.Equals(symbols.State, "unavailable", StringComparison.Ordinal);
        _files = (symbols.Files ?? []).ToDictionary(static f => f.Path, StringComparer.Ordinal);
    }

    /// <summary>"from 7c3e1a2", "from abcdef0, not 1234567", "index not built", ...</summary>
    internal string Caption { get; }

    /// <summary>The whole as-of words ("from abcdef0, not 1234567") when the index and the map are at different commits, for the note line; else null (#3996).</summary>
    internal string? AsOfNote { get; }

    /// <summary>Why an index that answers has no names for these files ("none in these files"), or null when it has some (#3996).</summary>
    internal string? EmptyReason { get; }

    /// <summary>The hover text of a dash cell: why the file has no names, when that is known (the code graph is off or did not build), else null (#3996).</summary>
    internal string? NoRowTip { get; }

    /// <summary>Whether every cell is drawn dimmed (stale or as-of mismatch).</summary>
    internal bool Dimmed { get; }

    /// <summary>Whether the index cannot answer: every cell says the caption instead of names.</summary>
    internal bool Unavailable { get; }

    /// <summary>The note line's symbols words: "symbols · from abcdef0, not 1234567 · none in these files"; a graph reason is whole sentences, so it stands alone, after the commits when they differ (#3862).</summary>
    /// <param name="column">The column's heading word.</param>
    /// <param name="anyDash">Whether some shown file row has a dash cell: with the graph off or down, a mixed folder (some .cs, some .ts) still needs the sentence for its dashes.</param>
    /// <returns>The words, or empty when there is nothing to say.</returns>
    internal string Note(string column, bool anyDash = false)
    {
        var graphSentence = _reasonIsSentence ? EmptyReason : anyDash ? _graphWhy : null;
        if (graphSentence is { } sentence)
        {
            return AsOfNote is null ? sentence : Labels.Dots(column, AsOfNote) + ". " + sentence;
        }

        return AsOfNote is null && EmptyReason is null ? string.Empty : Labels.Dots(column, AsOfNote, EmptyReason);
    }

    /// <summary>Whether any of these entries is a file whose cell is a dash.</summary>
    /// <param name="entries">The entries shown.</param>
    /// <returns>True when a file row would show a dash.</returns>
    internal bool AnyDash(IEnumerable<FileMapEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return !Unavailable && entries.Any(e => !string.Equals(e.Type, "dir", StringComparison.Ordinal) && string.Equals(Cell(e.Path), Labels.ContextSymbolsNoRow, StringComparison.Ordinal));
    }

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

    // Why only C# files get names, with the fix for each reason (#3862): the profile turned the graph off, or the graph did not build; null when the graph is on and up.
    private static string? GraphWhy(string? reason) => reason switch
    {
        "graph-off" => Labels.ContextSymbolsGraphOff,
        "graph-unavailable" => Labels.ContextSymbolsGraphUnavailable,
        _ => null,
    };
}
