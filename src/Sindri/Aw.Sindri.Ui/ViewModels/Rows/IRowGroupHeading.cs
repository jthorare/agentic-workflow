using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>What a group header of RowList shows, on top of the rows an <see cref="IRowGroup"/> holds.</summary>
internal interface IRowGroupHeading : IRowGroup, INotifyPropertyChanged
{
    /// <summary>The heading, in sentence case. Raise PropertyChanged when it changes: the header follows.</summary>
    public string Heading { get; }

    /// <summary>The right-hand summary, if any.</summary>
    public string? Summary { get; }

    /// <summary>True while the group is folded: the header and its count show, the rows do not. The header's chevron sets it; the page may set it too (Home folds Moving by default).</summary>
    public bool IsCollapsed { get; set; }
}
