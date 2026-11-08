using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One todo row of the panel (todo spec §3.1).</summary>
internal sealed class TodoRowViewModel : ObservableObject
{
    internal TodoRowViewModel(TodoView todo, DateOnly today)
    {
        _ = (todo, today);
    }

    /// <summary>The todo's "td_…" id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The text.</summary>
    public string Text { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The version a write sends back.</summary>
    public long Version { get; private set => Set(ref field, value); }

    /// <summary>The link refs drawn as chips: the first three.</summary>
    public IReadOnlyList<string> Chips { get; private set => Set(ref field, value); } = [];

    /// <summary>"+N" when more than three links; empty otherwise.</summary>
    public string MoreChips { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"agent", "edited by an agent", or empty.</summary>
    public string Tag { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The due word on the right: "Overdue · Mon", "Today", "Fri", "11-20", or empty.</summary>
    public string DueWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when open and due before today.</summary>
    public bool IsOverdue { get; private set => Set(ref field, value); }

    /// <summary>True when ticked.</summary>
    public bool IsDone { get; private set => Set(ref field, value); }

    /// <summary>True when removed.</summary>
    public bool IsRemoved { get; private set => Set(ref field, value); }

    /// <summary>True while a both-places conflict is open on it.</summary>
    public bool HasConflict { get; private set => Set(ref field, value); }

    /// <summary>The sentence a screen reader gets for the row.</summary>
    public string AccessibleName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Shows the todo as a new read serves it.</summary>
    /// <param name="todo">The todo.</param>
    /// <param name="today">Today's day in local time.</param>
    internal void Update(TodoView todo, DateOnly today) => throw new NotSupportedException();
}
