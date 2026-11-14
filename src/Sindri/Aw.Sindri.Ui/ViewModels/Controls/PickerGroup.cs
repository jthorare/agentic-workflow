namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>A titled run of choices in a <see cref="Views.Controls.ListPicker"/>.</summary>
/// <param name="Name">The group's heading; empty for an ungrouped list.</param>
/// <param name="Items">The choices, top to bottom.</param>
internal sealed record PickerGroup(string Name, IReadOnlyList<PickerItem> Items);
