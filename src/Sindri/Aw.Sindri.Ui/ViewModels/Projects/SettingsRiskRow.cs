namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One risky path in the editor: its globs (one per line) and why it is risky.</summary>
/// <param name="index">The row's place, from 1, for the screen-reader names.</param>
/// <param name="globsText">The globs, one per line.</param>
/// <param name="why">The reason shown beside the paths.</param>
internal sealed class SettingsRiskRow(int index, string globsText, string why) : ObservableObject
{
    /// <summary>The row's place, from 1.</summary>
    public int Index { get; } = index;

    /// <summary>The globs, one per line.</summary>
    public string GlobsText { get; set => Set(ref field, value); } = globsText;

    /// <summary>The reason shown beside the paths.</summary>
    public string Why { get; set => Set(ref field, value); } = why;

    /// <summary>The globs field's name.</summary>
    public string GlobsName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsRiskRow, Index, Labels.ReviewSettingsFieldGlobs);

    /// <summary>The reason field's name.</summary>
    public string WhyName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsRiskRow, Index, Labels.ReviewSettingsFieldWhy);

    /// <summary>The remove button's name.</summary>
    public string RemoveName => Labels.ReviewSettingsRemoveRow(Labels.ReviewSettingsRiskRow, Index);

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The remove button's text.</summary>
    public string RemoveText => Labels.ReviewSettingsRemove;
#pragma warning restore CA1822, MA0041

    /// <summary>The same row at another place.</summary>
    /// <param name="place">The new place, from 1.</param>
    /// <returns>The copy.</returns>
    internal SettingsRiskRow At(int place) => new(place, GlobsText, Why);
}
