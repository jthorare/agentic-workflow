namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>One name and value in Settings' Sandbox section.</summary>
/// <param name="Label">The name.</param>
/// <param name="Value">The value.</param>
internal sealed record SandboxField(string Label, string Value)
{
    /// <summary>The accessible name of the row: the name, then the value.</summary>
    public string Name => Label + ": " + Value;
}
