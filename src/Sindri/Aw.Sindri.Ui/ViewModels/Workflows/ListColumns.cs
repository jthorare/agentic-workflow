namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The columns the Workflows list keeps at a width (F3 workflows spec §2 "List"). There are no container queries in Avalonia, so the page
/// measures the list and asks here: from 960 up all six columns, under 960 Stage and Waiting on go, under 560 Step, Updated and the
/// reference under each title go too. Pure, so the bands are tested without a window.
/// </summary>
internal sealed class ListColumns
{
    /// <summary>The Item column: the title, and the reference under it.</summary>
    internal const int Item = 0;

    /// <summary>The Stage column.</summary>
    internal const int Stage = 1;

    /// <summary>The Status column.</summary>
    internal const int Status = 2;

    /// <summary>The Step column: the run's current step.</summary>
    internal const int Step = 3;

    /// <summary>The Waiting on column.</summary>
    internal const int WaitingOn = 4;

    /// <summary>The Updated column.</summary>
    internal const int Updated = 5;

    private const double FullFrom = 960;
    private const double MediumFrom = 560;

    private static readonly ListColumns _full = new(
        [new(2.2, IsStar: true, 200), new(128, IsStar: false, 0), new(148, IsStar: false, 0), new(1.8, IsStar: true, 150), new(124, IsStar: false, 0), new(80, IsStar: false, 0)],
        reference: true);

    private static readonly ListColumns _medium = new(
        [new(2, IsStar: true, 200), default, new(148, IsStar: false, 0), new(1.4, IsStar: true, 150), default, new(72, IsStar: false, 0)],
        reference: true);

    private static readonly ListColumns _narrow = new(
        [new(1, IsStar: true, 0), default, new(148, IsStar: false, 0), default, default, default],
        reference: false);

    private ListColumns(IReadOnlyList<ColumnSize> sizes, bool reference)
    {
        Sizes = sizes;
        ShowsReference = reference;
    }

    /// <summary>The six columns' widths, in column order; a hidden column is 0 wide.</summary>
    internal IReadOnlyList<ColumnSize> Sizes { get; }

    /// <summary>True while the mono reference shows under each title.</summary>
    internal bool ShowsReference { get; }

    /// <summary>The columns for a list this wide.</summary>
    /// <param name="width">The list's width in pixels; 0 or less (not measured yet) keeps the full band.</param>
    /// <returns>One of three shared instances, so a change is a reference change.</returns>
    internal static ListColumns For(double width) => width switch
    {
        <= 0 => _full,
        < MediumFrom => _narrow,
        < FullFrom => _medium,
        _ => _full,
    };

    /// <summary>Whether a column shows.</summary>
    /// <param name="column">The column's index.</param>
    /// <returns>True when its width is not 0.</returns>
    internal bool Shows(int column) => Sizes[column].Value > 0;
}
