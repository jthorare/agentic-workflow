namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>One choice in a <see cref="Views.Controls.ListPicker"/>: a block to add, a step to connect to.</summary>
/// <param name="Id">The choice's id, handed back when it is picked.</param>
/// <param name="Text">What the row says; it is also the row's accessible name.</param>
/// <param name="IsEnabled">False for a choice that cannot be picked now; <paramref name="Reason"/> says why.</param>
/// <param name="Reason">Why a disabled choice is off ("needs a connection"), as the row's tip; null when enabled.</param>
internal sealed record PickerItem(string Id, string Text, bool IsEnabled = true, string? Reason = null);
