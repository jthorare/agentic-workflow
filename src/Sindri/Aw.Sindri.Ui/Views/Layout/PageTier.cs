namespace Aw.Sindri.Ui.Views.Layout;

/// <summary>How wide the page region is (proof B8): the rows and Home choose their columns from it.</summary>
internal enum PageTier
{
    /// <summary>1120 px and up: every column.</summary>
    Wide,

    /// <summary>1000 up to 1120: since and step go.</summary>
    Mid,

    /// <summary>760 up to 1000: repo goes too, and Home stacks its panels.</summary>
    Narrow,

    /// <summary>Under 790: only id, title and the primary action stay (state is in the row's name and the sheet).</summary>
    Tight,
}
