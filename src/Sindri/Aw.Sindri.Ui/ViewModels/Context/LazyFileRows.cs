using System.Collections;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The rows of a folder, built when the list asks for them: a folder of tens of thousands of entries makes a row view model
/// only for the ones scrolled to. The entries are the page-source seam: the pages read so far, with the count the server
/// says there are in all; a position past what is read is a placeholder row, never a wait. A filter is a list of positions
/// into the entries read, so narrowing copies no entry.
/// </summary>
internal sealed class LazyFileRows : IReadOnlyList<FileRowViewModel>
{
    private readonly IReadOnlyList<FileMapEntry> _entries;
    private readonly int[]? _positions;
    private readonly SymbolsColumnViewModel? _symbols;
    private readonly FileRowViewModel?[] _built;
    private readonly Action<int>? _touched;

    /// <summary>Rows over entries; with positions, only those entries in that order.</summary>
    /// <param name="entries">The entries read so far (a snapshot nobody appends to).</param>
    /// <param name="symbols">The folder's symbols column, or null.</param>
    /// <param name="positions">Which entries to show, or null for all.</param>
    /// <param name="total">How many entries there are in all, or null when all are read.</param>
    /// <param name="touched">Called with the position of each row the list asks for (unfiltered lists only), so the caller can read the next page ahead of the scroll.</param>
    internal LazyFileRows(IReadOnlyList<FileMapEntry> entries, SymbolsColumnViewModel? symbols, int[]? positions = null, int? total = null, Action<int>? touched = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        _symbols = symbols;
        _positions = positions;
        _touched = positions is null ? touched : null;
        Count = positions?.Length ?? Math.Max(entries.Count, total ?? 0);
        _built = new FileRowViewModel?[Count];
    }

    /// <summary>How many rows the list holds.</summary>
    public int Count { get; }

    /// <summary>How many entries are read.</summary>
    internal int Loaded => _entries.Count;

    /// <summary>How many row view models exist so far (placeholders are not counted).</summary>
    internal int Built => _built.Count(static r => r is not null);

    /// <summary>The row at a position: built on first use, or a placeholder when its page is not read yet.</summary>
    /// <param name="index">The position.</param>
    public FileRowViewModel this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            _touched?.Invoke(index);
            var position = _positions?[index] ?? index;
            return position >= _entries.Count ? FileRowViewModel.Placeholder() : _built[index] ??= new FileRowViewModel(_entries[position], _symbols);
        }
    }

    /// <summary>The entries read, narrowed to the names that contain some text.</summary>
    /// <param name="text">The text, any case; empty keeps every entry.</param>
    /// <param name="total">How many entries there are in all (unfiltered result only).</param>
    /// <param name="touched">The unfiltered result's touch callback.</param>
    /// <returns>The narrowed list.</returns>
    internal LazyFileRows Narrow(string text, int? total = null, Action<int>? touched = null)
    {
        if (text.Length == 0)
        {
            return new LazyFileRows(_entries, _symbols, positions: null, total, touched);
        }

        var kept = new List<int>();
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(i);
            }
        }

        return new LazyFileRows(_entries, _symbols, [.. kept]);
    }

    /// <inheritdoc />
    public IEnumerator<FileRowViewModel> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
