namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One diff group in the editor: its name, its globs (one per line) and whether it shows folded.</summary>
/// <param name="index">The row's place, from 1, for the screen-reader names.</param>
/// <param name="name">The group's heading.</param>
/// <param name="globsText">The globs, one per line.</param>
/// <param name="folded">True when the group shows collapsed.</param>
internal sealed class SettingsGroupRow(int index, string name, string globsText, bool folded) : ObservableObject
{
    /// <summary>The row's place, from 1.</summary>
    public int Index { get; } = index;

    /// <summary>The group's heading.</summary>
    public string Name { get; set => Set(ref field, value); } = name;

    /// <summary>The globs, one per line.</summary>
    public string GlobsText { get; set => Set(ref field, value); } = globsText;

    /// <summary>True when the group shows collapsed.</summary>
    public bool Folded { get; set => Set(ref field, value); } = folded;

    /// <summary>The name field's name.</summary>
    public string NameName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsGroupRow, Index, Labels.ReviewSettingsFieldName);

    /// <summary>The globs field's name.</summary>
    public string GlobsName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsGroupRow, Index, Labels.ReviewSettingsFieldGlobs);

    /// <summary>The folded box's name.</summary>
    public string FoldedName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsGroupRow, Index, Labels.ReviewSettingsFieldFolded);

    /// <summary>The remove button's name.</summary>
    public string RemoveName => Labels.ReviewSettingsRemoveRow(Labels.ReviewSettingsGroupRow, Index);

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The folded box's text.</summary>
    public string FoldedText => Labels.ReviewSettingsFieldFolded;

    /// <summary>The remove button's text.</summary>
    public string RemoveText => Labels.ReviewSettingsRemove;
#pragma warning restore CA1822, MA0041

    /// <summary>The same row at another place.</summary>
    /// <param name="place">The new place, from 1.</param>
    /// <returns>The copy.</returns>
    internal SettingsGroupRow At(int place) => new(place, Name, GlobsText, Folded);
}
