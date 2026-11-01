namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>The two read-only Jev pick lines Settings shows under the model-choice line (model routing spec 8.5), handed to the page together.</summary>
/// <param name="Label">The label-choice line.</param>
/// <param name="Surface">The product-surface-choice line.</param>
internal sealed record PickLines(LabelChoiceViewModel Label, SurfaceChoiceViewModel Surface);
