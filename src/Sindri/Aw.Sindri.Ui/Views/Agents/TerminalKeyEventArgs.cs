namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>A key that goes to the agent: the route name and the text the key typed, if any.</summary>
internal sealed class TerminalKeyEventArgs(string name, string? text) : EventArgs
{
    /// <summary>The route name, as <see cref="TerminalKeyNames"/> gives it.</summary>
    public string Name { get; } = name;

    /// <summary>The characters the key typed; null for a named key.</summary>
    public string? Text { get; } = text;
}
