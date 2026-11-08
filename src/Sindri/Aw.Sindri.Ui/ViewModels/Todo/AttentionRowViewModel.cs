using System.Globalization;
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

    private const int SummaryChanges = 3;
    private const int ListedChanges = 5;

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

    /// <summary>What decides whether a read changed the row: the same signature keeps the row (its open form and notice) as it is.</summary>
    internal string Signature { get; private set; } = string.Empty;

    /// <summary>The row of a todo with an open both-places conflict.</summary>
    /// <param name="todo">The todo; its <see cref="TodoView.Conflict"/> is not null.</param>
    /// <returns>The row.</returns>
    internal static AttentionRowViewModel ForConflict(TodoView todo)
    {
        var conflict = todo.Conflict ?? throw new ArgumentException("The todo has no open conflict.", nameof(todo));
        var (mine, theirs) = (conflict.Sindri, conflict.Obsidian);
        var (sindriFields, obsidianFields, same) = Compare(mine, theirs);
        var row = new AttentionRowViewModel(AttentionKind.Conflict, todo.Id)
        {
            Line = Labels.TodoAttnConflictLine(todo.Text),
            EnterHint = Labels.TodoAttnSortOutHint,
            Title = Labels.TodoAttnConflictTitle,
            Meta = same.Count == 0 ? string.Empty : string.Join(" · ", same) + ", " + Labels.TodoAttnSameInBoth,
            Version = todo.Version,
            AccessibleName = Labels.TodoAttnConflictName(todo.Text),
            Choices =
            [
                new AttentionChoiceViewModel(
                    "1",
                    Labels.TodoAttnSindri,
                    mine.Removed ? Labels.TodoAttnKeepRemoved : Labels.TodoAttnKeepSindri,
                    Labels.TodoAttnKeepName("Sindri's", mine.Removed ? null : mine.Text),
                    sindriFields,
                    mine.Removed),
                new AttentionChoiceViewModel(
                    "2",
                    Labels.TodoAttnObsidian,
                    theirs.Removed ? Labels.TodoAttnRemoveIt : Labels.TodoAttnKeepObsidian,
                    theirs.Removed ? Labels.TodoAttnRemoveName(todo.Text) : Labels.TodoAttnKeepName("Obsidian's", theirs.Text),
                    obsidianFields,
                    theirs.Removed),
            ],
        };
        row.Signature = string.Join('\u001f', row.Kind, row.Id, row.Line, row.Meta, row.Version, conflict.NoteHash, string.Join('\u001e', row.Choices.SelectMany(c => c.Fields.Select(f => f.Label + f.Value))));
        return row;
    }

    /// <summary>The row of the open hold.</summary>
    /// <param name="hold">The hold.</param>
    /// <returns>The row.</returns>
    internal static AttentionRowViewModel ForHold(TodoHoldView hold)
    {
        var count = hold.Changes.Count;
        var summary = Labels.TodoAttnHoldSummary([.. hold.Changes.Take(SummaryChanges).Select(c => Labels.TodoAttnHoldChange(c.Kind, c.Text))], Math.Max(count - SummaryChanges, 0));
        var row = new AttentionRowViewModel(AttentionKind.Hold, HoldId)
        {
            Line = Labels.TodoAttnHoldTitle(count),
            EnterHint = Labels.TodoAttnLookHint,
            Title = Labels.TodoAttnHoldTitle(count),
            Meta = summary,
            Changes = [.. hold.Changes.Take(ListedChanges).Select(c => Labels.TodoAttnHoldChangeLine(c.Kind, c.Text))],
            MoreChanges = count > ListedChanges ? Labels.TodoAttnHoldMore(count - ListedChanges) : string.Empty,
            HeldCount = count,
            NoteHash = hold.NoteHash,
            AccessibleName = Labels.TodoAttnHoldName(count, summary),
            Choices =
            [
                new AttentionChoiceViewModel("1", string.Empty, Labels.TodoAttnPutBack, Labels.TodoAttnPutBackName(count), [], isRemoved: false),
                new AttentionChoiceViewModel("2", string.Empty, Labels.TodoAttnApply, Labels.TodoAttnApplyName(count), [], isRemoved: false),
            ],
        };
        row.Signature = string.Join('\u001f', row.Kind, row.Id, row.Line, row.Meta, hold.NoteHash, string.Join('\u001e', row.Changes));
        return row;
    }

    // The fields where the two versions differ, per column, and the values they share (shown once, dimmed). A removal makes the removed column say so and the other show its own words.
    private static (List<AttentionFieldViewModel> Sindri, List<AttentionFieldViewModel> Obsidian, List<string> Same) Compare(TodoSideView mine, TodoSideView theirs)
    {
        if (mine.Removed || theirs.Removed)
        {
            return (OneSide(mine), OneSide(theirs), []);
        }

        Columns columns = new([], [], []);
        Field(Labels.TodoAttnText, mine.Text ?? string.Empty, theirs.Text ?? string.Empty, columns);
        Field(Labels.TodoAttnDue, DueWord(mine.Due), DueWord(theirs.Due), columns);
        Field(Labels.TodoAttnLinks, LinksWord(mine), LinksWord(theirs), columns, dimWhenEmpty: true);
        Field(Labels.TodoAttnState, StateWord(mine), StateWord(theirs), columns);
        return (columns.Left, columns.Right, columns.Same);
    }

    private static List<AttentionFieldViewModel> OneSide(TodoSideView side) =>
        side.Removed
            ? [new AttentionFieldViewModel(Labels.TodoAttnState, Labels.TodoAttnRemoved)]
            : [new AttentionFieldViewModel(Labels.TodoAttnText, side.Text ?? string.Empty), new AttentionFieldViewModel(Labels.TodoAttnState, StateWord(side))];

    private static void Field(string label, string mine, string theirs, Columns columns, bool dimWhenEmpty = false)
    {
        if (!string.Equals(mine, theirs, StringComparison.Ordinal))
        {
            columns.Left.Add(new AttentionFieldViewModel(label, mine));
            columns.Right.Add(new AttentionFieldViewModel(label, theirs));
        }
        else if (!(dimWhenEmpty && string.Equals(mine, Labels.TodoAttnNoLinks, StringComparison.Ordinal)))
        {
            columns.Same.Add(mine);
        }
    }

    // The two columns' differing fields and the values both versions share, filled one field at a time.
    private sealed record Columns(List<AttentionFieldViewModel> Left, List<AttentionFieldViewModel> Right, List<string> Same);

    private static string DueWord(DateOnly? due) => due is { } day ? day.ToString("ddd MM-dd", CultureInfo.InvariantCulture) : Labels.TodoAttnNoDue;

    private static string LinksWord(TodoSideView side) => side.Links is { Count: > 0 } links ? string.Join(", ", links) : Labels.TodoAttnNoLinks;

    private static string StateWord(TodoSideView side) => side.Removed ? Labels.TodoAttnRemoved : side.Done ? Labels.TodoAttnDone : Labels.TodoAttnNotDone;
}
