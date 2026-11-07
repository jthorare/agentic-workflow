namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>A row asks for its issue's sheet (a click, Enter, a strip row).</summary>
/// <param name="key">The issue key, such as ENG-12.</param>
internal sealed class LinearIssueRequestedEventArgs(string key) : EventArgs
{
    /// <summary>The issue key.</summary>
    public string Key { get; } = key;
}
