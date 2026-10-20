namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One Step run in the Runs list: its sentence and the accessible name that says the same in words.</summary>
/// <param name="Text">The visible sentence.</param>
/// <param name="Name">The accessible name; it starts with the visible words.</param>
internal sealed record StepRunRowViewModel(string Text, string Name);
