using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>One tab in Home's header: its split, its label and how many open rows of its kinds Home holds.</summary>
/// <param name="Split">The split the tab selects.</param>
/// <param name="Label">The tab's word.</param>
/// <param name="Count">Open rows of the split's kinds.</param>
/// <param name="IsSelected">True on the tab whose rows show.</param>
internal sealed record HomeTab(HomeSplit Split, string Label, int Count, bool IsSelected)
{
    /// <summary>The count as the tab draws it.</summary>
    public string CountText { get; } = Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a screen reader says for the tab.</summary>
    public string Name { get; } = Labels.HomeTabName(Label, Count);
}
