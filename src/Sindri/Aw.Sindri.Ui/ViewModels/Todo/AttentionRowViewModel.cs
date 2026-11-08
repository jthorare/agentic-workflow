using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// An attention row of the Todo panel (todo spec §3.1, D13): one both-places conflict, or the hold on a mass change from the note. Folded it is one line
/// ("Changed in Sindri and in Obsidian: reply to Sam. Enter to sort it out"); open it is two columns with a button each, drawn at its full height.
/// </summary>
internal sealed class AttentionRowViewModel : ObservableObject
{
    private AttentionRowViewModel(AttentionKind kind, string id)
    {
        (Kind, Id) = (kind, id);
    }

    /// <summary>The key a todo's hold row goes by in <see cref="Id"/>.</summary>
    internal const string HoldId = "hold";

    /// <summary>A conflict or a hold.</summary>
    public AttentionKind Kind { get; }

    /// <summary>The todo's "td_…" id for a conflict; "hold" for the hold.</summary>
    public string Id { get; }

    /// <summary>True for a conflict row.</summary>
    public bool IsConflict => Kind == AttentionKind.Conflict;

    /// <summary>True for the hold row.</summary>
    public bool IsHold => Kind == AttentionKind.Hold;

    /// <summary>The folded line: "Changed in Sindri and in Obsidian: reply to Sam" or "The note changed 14 todos.".</summary>
    public string Line { get; private set; } = string.Empty;

    /// <summary>The key beside the folded line ("Enter").</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string EnterKey => Labels.TodoAttnEnterKey;
#pragma warning restore CA1822, MA0041

    /// <summary>The words after the key: "to sort it out" or "to look".</summary>
    public string EnterHint { get; private set; } = string.Empty;

    /// <summary>The open row's title: "Changed in Sindri and in Obsidian", or the hold's "The note changed 14 todos.".</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The open conflict's dimmed line of what both versions agree on: "reply to Sam · due Fri 11-13 · Not done, same in both"; the hold's summary of its first three changes.</summary>
    public string Meta { get; private set; } = string.Empty;

    /// <summary>The two columns of an open conflict (Sindri, Obsidian) or the two buttons of a hold (Put them back, Apply), in key order: 1 then 2.</summary>
    public IReadOnlyList<AttentionChoiceViewModel> Choices { get; private set; } = [];

    /// <summary>An open hold's changes, one line each: the first five.</summary>
    public IReadOnlyList<string> Changes { get; private set; } = [];

    /// <summary>"and 9 more" past the fifth change; empty otherwise.</summary>
    public string MoreChanges { get; private set; } = string.Empty;

    /// <summary>How many changes the hold holds; 0 for a conflict.</summary>
    public int HeldCount { get; private set; }

    /// <summary>The hold's noteHash, sent back by Apply and Put them back; empty for a conflict.</summary>
    public string NoteHash { get; private set; } = string.Empty;

    /// <summary>The conflict's todo version; 0 for the hold.</summary>
    public long Version { get; private set; }

    /// <summary>True while the row is open to its full form.</summary>
    public bool IsOpen { get; internal set => Set(ref field, value); }

    /// <summary>The reason a pick failed, or "Pick a version first"; empty otherwise.</summary>
    public string Notice { get; internal set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence a screen reader gets for the row (§3.5).</summary>
    public string AccessibleName { get; private set; } = string.Empty;

    /// <summary>The row of a todo with an open both-places conflict.</summary>
    /// <param name="todo">The todo; its <see cref="TodoView.Conflict"/> is not null.</param>
    /// <param name="today">Today's day in local time (the due words).</param>
    /// <returns>The row.</returns>
    internal static AttentionRowViewModel ForConflict(TodoView todo, DateOnly today)
    {
        _ = (new AttentionRowViewModel(AttentionKind.Conflict, todo.Id), today);
        throw new NotSupportedException();
    }

    /// <summary>The row of the open hold.</summary>
    /// <param name="hold">The hold.</param>
    /// <returns>The row.</returns>
    internal static AttentionRowViewModel ForHold(TodoHoldView hold) => throw new NotSupportedException();
}
