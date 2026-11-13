using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>
/// A row as RowList draws it: a subset of the API's work row, so the adapter is a plain map. A row updates in place two ways: raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> (the row redraws its text), or replace the item with a new object that has the same <see cref="Key"/>
/// (the list swaps that one container's data and keeps focus and the peek).
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal interface IRowItem : INotifyPropertyChanged
{
    /// <summary>The stable identity: the API's source:kind:native id. Selection, the peek, the cursor and diffs match on it, never on object reference.</summary>
    public string Key { get; }

    /// <summary>The display id (#4182, BIL-301, r-0412).</summary>
    public string Id { get; }

    /// <summary>The title.</summary>
    public string Title { get; }

    /// <summary>The sub-line shown after the title, if any.</summary>
    public string? SubLine { get; }

    /// <summary>The row's kind: pr, issue, run, thread, session, item, decision, note.</summary>
    public string Kind { get; }

    /// <summary>The state class; with <see cref="Kind"/> it picks the glyph (<see cref="RowGlyphs.For"/>).</summary>
    public RowStateClass StateClass { get; }

    /// <summary>The API's state key, for glyphs the class alone cannot pick; null when none.</summary>
    public string? StateKey { get; }

    /// <summary>True when unread: the title is weight 500, a read one is muted.</summary>
    public bool Unread { get; }

    /// <summary>The facts by column key; a missing key is a blank cell.</summary>
    public IReadOnlyDictionary<string, RowFact> Facts { get; }

    /// <summary>The age text, right-aligned.</summary>
    public string Age { get; }

    /// <summary>Who the row waits on; null on a finished row.</summary>
    public RowWaitsOn? WaitsOn { get; }

    /// <summary>The 2 or 3 buttons shown on hover and under the cursor.</summary>
    public IReadOnlyList<RowAction> Actions { get; }
}
