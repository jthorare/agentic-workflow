namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>A link of a todo was chosen: the deep link to go to.</summary>
/// <param name="href">The link, as the todo carries it ("sindri://linear/ENG-12").</param>
internal sealed class TodoLinkOpenEventArgs(string href) : EventArgs
{
    /// <summary>The link.</summary>
    internal string Href { get; } = href;
}
