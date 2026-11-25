namespace Aw.Sindri.Ui.ViewModels;

/// <summary>One starter template in the New workflow form.</summary>
/// <param name="Name">The name Sindri accepts.</param>
/// <param name="Title">The title the picker shows ("Weekday check").</param>
/// <param name="Description">What it does, in one line.</param>
internal sealed record TemplateChoice(string Name, string Title, string Description);
