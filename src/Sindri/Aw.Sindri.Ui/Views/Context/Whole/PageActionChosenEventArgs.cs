namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>An action's name was pressed in the Pages and actions section.</summary>
/// <param name="key">The action's node key.</param>
internal sealed class PageActionChosenEventArgs(string key) : EventArgs
{
    /// <summary>Gets the action's node key.</summary>
    public string Key { get; } = key;
}
