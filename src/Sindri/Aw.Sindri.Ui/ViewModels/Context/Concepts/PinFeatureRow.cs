namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One feature on the Pin sheet.</summary>
/// <param name="Key">The feature's exact key.</param>
/// <param name="Label">Its label.</param>
/// <param name="Ticked">Whether it goes into the pin's features.</param>
internal sealed record PinFeatureRow(string Key, string Label, bool Ticked);
