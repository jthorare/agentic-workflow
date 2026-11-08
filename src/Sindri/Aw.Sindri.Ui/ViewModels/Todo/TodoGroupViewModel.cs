using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>A group of the expanded panel: Overdue, Today, Upcoming, No date, Done or Removed.</summary>
internal sealed class TodoGroupViewModel(string heading) : ObservableObject
{
    /// <summary>The heading.</summary>
    public string Heading { get; } = heading;

    /// <summary>The rows, in order.</summary>
    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];

    /// <summary>Shows these rows.</summary>
    /// <param name="rows">The rows, kept in place when already shown.</param>
    internal void Show(IReadOnlyList<TodoRowViewModel> rows) => throw new NotSupportedException();
}
