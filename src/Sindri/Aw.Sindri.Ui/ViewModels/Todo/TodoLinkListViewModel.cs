using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The small list O opens on a row with more than one link (todo spec §3.3): J and K move, Enter opens, Esc closes.</summary>
internal sealed class TodoLinkListViewModel : ObservableObject
{
    /// <summary>True while the list shows.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The links of the todo it was opened on.</summary>
    public IReadOnlyList<TodoLinkItemViewModel> Items { get; private set => Set(ref field, value); } = [];

    /// <summary>The line J, K and Enter act on.</summary>
    public int SelectedIndex { get; private set => Set(ref field, value); }

    /// <summary>Shows the list with the first line selected.</summary>
    /// <param name="links">The todo's links.</param>
    internal void Open(IReadOnlyList<TodoLinkView> links) => throw new NotSupportedException();

    /// <summary>Moves the selection; the first and the last line hold.</summary>
    /// <param name="step">1 for J, -1 for K.</param>
    internal void Move(int step) => throw new NotSupportedException();

    /// <summary>Closes the list.</summary>
    /// <returns>True when it was open.</returns>
    internal bool Close() => throw new NotSupportedException();
}
