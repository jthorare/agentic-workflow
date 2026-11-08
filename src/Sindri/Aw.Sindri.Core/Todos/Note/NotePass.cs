namespace Aw.Sindri.Core.Todos.Note;

/// <summary>One merge: the state of a pass over the base, the note and Sindri (todo spec §4.5). Built and run once by <see cref="NoteMerge"/>.</summary>
internal sealed partial class NotePass
{
    private readonly bool _hasBase;
    private readonly NoteDocument _note;
    private readonly IReadOnlyList<SindriTodo> _sindriList;
    private readonly NoteMergeOptions _options;
    private readonly Dictionary<TodoId, BaseEntry> _base = [];
    private readonly List<TodoId> _baseOrder = [];
    private readonly Dictionary<TodoId, SindriTodo> _sindri = [];
    private readonly Dictionary<TodoId, NoteTodoLine> _noteFirst = [];
    private readonly List<Slot> _slots = [];
    private readonly List<NoteLineEdit> _edits = [];
    private readonly Dictionary<TodoId, string?> _nextLine = [];
    private readonly List<TodoId> _newOrder = [];
    private int _adopted;
    private int _deferred;

    public NotePass(NoteBase? noteBase, NoteDocument note, IReadOnlyList<SindriTodo> sindri, NoteMergeOptions options)
    {
        _hasBase = noteBase is not null;
        _note = note;
        _sindriList = sindri;
        _options = options;
        foreach (var line in noteBase?.Lines ?? [])
        {
            if (NoteTodoLineReader.IsTaskLine(line) && NoteTodoLineReader.Read(0, line) is { Id: { } id } parsed && !_base.ContainsKey(id))
            {
                _base[id] = new BaseEntry(line, FieldsOf(parsed));
                _baseOrder.Add(id);
            }
        }

        foreach (var todo in sindri)
        {
            _ = _sindri.TryAdd(todo.Id, todo);
        }

        foreach (var todo in note.Todos.Where(static t => t.Id is not null))
        {
            _ = _noteFirst.TryAdd(todo.Id!.Value, todo); // why: filtered on the line before
        }
    }

    public NoteMergeResult Run()
    {
        foreach (var todo in _note.Todos)
        {
            if (todo.Id is { } id && _noteFirst[id].Number == todo.Number)
            {
                KnownLine(todo, id);
            }
            else
            {
                NewLine(todo);
            }
        }

        foreach (var id in _baseOrder.Where(id => !_noteFirst.ContainsKey(id)))
        {
            MissingLine(id);
        }

        AppendFromSindri();
        return Finish();
    }

    private NoteMergeResult Finish()
    {
        var counted = _slots.Where(static s => s.Candidate is not null).Select(static s => s.Candidate!).ToList(); // why: filtered on the line before
        var baseOpen = _base.Values.Count(static b => !b.Fields.Done);
        var decision = HoldDecision.Decide(counted, baseOpen);
        var intents = new List<NoteIntent>();
        foreach (var slot in _slots)
        {
            var held = decision.Held && slot.Candidate is not null;
            intents.AddRange(held ? [] : slot.Intents);
            _nextLine[slot.Id] = held ? slot.OldBaseLine : slot.NewBaseLine;
        }

        var outcome = Outcome(decision.Held, counted);
        return new NoteMergeResult(intents, _edits, NextBase(), outcome, decision.Held ? new NoteHold(counted) : null, _adopted, _deferred);
    }

    private NoteHoldOutcome Outcome(bool held, List<HeldChange> counted)
    {
        var open = _options.OpenHold;
        if (!held)
        {
            return open is null ? NoteHoldOutcome.None : NoteHoldOutcome.Dissolved;
        }

        if (open is null)
        {
            return NoteHoldOutcome.Opened;
        }

        return open.Changes.Count == counted.Count && open.Changes.All(counted.Contains) ? NoteHoldOutcome.Unchanged : NoteHoldOutcome.Replaced;
    }

    private List<string> NextBase()
    {
        var lines = new List<string>();
        foreach (var id in _baseOrder)
        {
            var line = _nextLine.TryGetValue(id, out var next) ? next : _base[id].Line;
            if (line is not null)
            {
                lines.Add(line);
            }
        }

        lines.AddRange(_newOrder.Select(id => _nextLine[id]).OfType<string>());
        return lines;
    }

    private NoteFields FieldsOf(NoteTodoLine line) =>
        new(_options.Scrubber.Scrub(line.Text), line.Due, line.Links, line.Done, line.DoneOn);

    private void SetBase(TodoId id, string? line)
    {
        _nextLine[id] = line;
        if (!_base.ContainsKey(id) && !_newOrder.Contains(id))
        {
            _newOrder.Add(id);
        }
    }

    private void KeepBase(TodoId id)
    {
        if (_base.TryGetValue(id, out var entry))
        {
            _nextLine[id] = entry.Line;
        }
    }

    private void AddSlot(TodoId id, IEnumerable<NoteIntent> intents, HeldChange? candidate, string? newBaseLine)
    {
        _slots.Add(new Slot(id, [.. intents], candidate, newBaseLine, _base.TryGetValue(id, out var entry) ? entry.Line : null));
        if (!_base.ContainsKey(id) && !_newOrder.Contains(id))
        {
            _newOrder.Add(id);
        }
    }

    private sealed record BaseEntry(string Line, NoteFields Fields);

    private sealed record Slot(TodoId Id, IReadOnlyList<NoteIntent> Intents, HeldChange? Candidate, string? NewBaseLine, string? OldBaseLine);
}
