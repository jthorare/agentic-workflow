using System.Globalization;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One todo row of the panel (todo spec §3.1): the text, the link chips, the agent tag and the due word, with the sentence a screen reader gets (§3.5).</summary>
internal sealed class TodoRowViewModel : ObservableObject
{
    private const int ChipsDrawn = 3;
    private const int WeekdayWithin = 6;

    internal TodoRowViewModel(TodoView todo, DateOnly today)
    {
        Id = todo.Id;
        Update(todo, today);
    }

    /// <summary>The todo's "td_…" id.</summary>
    public string Id { get; }

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

    /// <summary>Shows the todo as a new read serves it, and the due word for today's day.</summary>
    /// <param name="todo">The todo.</param>
    /// <param name="today">Today's day in local time.</param>
    internal void Update(TodoView todo, DateOnly today)
    {
        var facts = Facts.Of(todo, today);
        Text = todo.Text;
        Version = todo.Version;
        Chips = [.. todo.Links.Take(ChipsDrawn).Select(l => l.Ref)];
        MoreChips = todo.Links.Count > ChipsDrawn ? Labels.TodoMoreChips(todo.Links.Count - ChipsDrawn) : string.Empty;
        Tag = facts.AddedByAgent ? Labels.TodoTagAgent : facts.EditedByAgent ? Labels.TodoTagEditedByAgent : string.Empty;
        IsOverdue = facts.Overdue;
        IsDone = !todo.Removed && todo.DoneAt is not null;
        IsRemoved = todo.Removed;
        HasConflict = todo.Conflict is not null;
        DueWord = facts.Due is { } due ? DueWordFor(due, facts) : string.Empty;
        AccessibleName = Sentence(todo, facts);
    }

    private static string DueWordFor(DateOnly due, Facts facts)
    {
        var day = Math.Abs(facts.Days) <= WeekdayWithin ? due.ToString("ddd", CultureInfo.InvariantCulture) : due.ToString("MM-dd", CultureInfo.InvariantCulture);
        return facts.Overdue ? Labels.TodoDueOverdue(day) : facts.Days == 0 ? Labels.TodoDueToday : day;
    }

    private static string Sentence(TodoView todo, Facts facts)
    {
        List<string> parts =
        [
            Labels.TodoRowName,
            todo.Text,
            facts.Due is { } due ? Labels.TodoDueName(facts.Overdue, LongDay(due, facts.Days)) : Labels.TodoNoDueDate,
            todo.Removed ? Labels.TodoRemovedState : facts.Open ? Labels.TodoNotDone : Labels.TodoDoneState,
        ];
        if (todo.Links.Count > 0)
        {
            parts.Add(Labels.TodoLinksName(todo.Links.Count));
        }

        if (facts.AddedByAgent)
        {
            parts.Add(Labels.TodoAddedByAgent);
        }
        else if (facts.EditedByAgent)
        {
            parts.Add(Labels.TodoTagEditedByAgent);
        }

        return string.Join(", ", parts);
    }

    private static string LongDay(DateOnly due, int days) =>
        days == 0 ? "today" : Math.Abs(days) <= WeekdayWithin ? due.ToString("dddd", CultureInfo.InvariantCulture) : due.ToString("MMMM d", CultureInfo.InvariantCulture);

    // What a row works out from the todo and today: its state, how far off its day is, and who touched it.
    private readonly record struct Facts(bool Open, DateOnly? Due, int Days, bool Overdue, bool AddedByAgent, bool EditedByAgent)
    {
        internal static Facts Of(TodoView todo, DateOnly today)
        {
            var open = !todo.Removed && todo.DoneAt is null;
            var days = todo.Due is { } due ? due.DayNumber - today.DayNumber : 0;
            var addedByAgent = todo.AddedBy.StartsWith("session:", StringComparison.Ordinal) || string.Equals(todo.AddedVia, "pipe", StringComparison.Ordinal);
            return new Facts(open, todo.Due, days, Overdue: open && days < 0, addedByAgent, EditedByAgent: !addedByAgent && todo.LastBy.StartsWith("session:", StringComparison.Ordinal));
        }
    }
}
