namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The panel's O key (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3): one link opens, several open a list.</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>Raised when a link is chosen; the shell goes to it.</summary>
    internal event EventHandler<TodoLinkOpenEventArgs>? LinkOpened;

    /// <summary>The list O opens on a row with several links.</summary>
    public TodoLinkListViewModel LinkList { get; } = new();

    /// <summary>O on a row: opens its only link, or the list of its links.</summary>
    /// <param name="id">The todo's id.</param>
    internal void OpenLinks(string id)
    {
        if (_todos?.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal)) is not { Links.Count: > 0 } todo)
        {
            return;
        }

        if (todo.Links.Count == 1)
        {
            OnLinkOpened(todo.Links[0].Href);
            return;
        }

        LinkList.Open(todo.Links);
    }

    /// <summary>Enter in the list: opens the selected link and closes the list.</summary>
    internal void OpenSelectedLink()
    {
        if (LinkList.Selected is not { } line)
        {
            return;
        }

        var href = line.Href;
        _ = LinkList.Close();
        OnLinkOpened(href);
    }

    private void OnLinkOpened(string href) => LinkOpened?.Invoke(this, new TodoLinkOpenEventArgs(href));
}
