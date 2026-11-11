namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One row of the diff's flat list (#2776): a group header, a file header, a hunk header or a line. A file's rows draw as one card, so a row knows whether it is the card's first or last.</summary>
internal abstract class ReviewDiffRow : ObservableObject
{
    /// <summary>True for the first row of a file's card: it draws the top edge and the rounded top corners.</summary>
    public bool IsFirst { get; internal set => Set(ref field, value); }

    /// <summary>True for the last row of a file's card: it draws the bottom edge, the rounded bottom corners and the gap below the card.</summary>
    public bool IsLast { get; internal set => Set(ref field, value); }
}
