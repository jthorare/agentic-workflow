namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>One tab in Home's header: its split, its label and how many open rows of its kinds Home holds.</summary>
/// <param name="Split">The split the tab selects.</param>
/// <param name="Label">The tab's word.</param>
/// <param name="Count">Open rows of the split's kinds.</param>
/// <param name="IsSelected">True on the tab whose rows show.</param>
internal sealed record HomeTab(HomeSplit Split, string Label, int Count, bool IsSelected);
