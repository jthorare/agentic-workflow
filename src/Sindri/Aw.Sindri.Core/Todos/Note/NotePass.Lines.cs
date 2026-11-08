namespace Aw.Sindri.Core.Todos.Note;

internal sealed partial class NotePass
{
    private static readonly NoteSide _removedSide = new(Text: null, Due: null, Links: [], Done: false, Removed: true);

    private void KnownLine(NoteTodoLine line, TodoId id)
    {
        if (_options.LeaveAlone.Contains(id))
        {
            KeepBase(id);
            return;
        }

        _ = _sindri.TryGetValue(id, out var todo);
        if (line.Break != NoteBreak.None)
        {
            if (todo is not null || _base.ContainsKey(id))
            {
                AddSlot(id, [new NoteLineBlocked(id, line.Number, NoteBreakReasons.Of(line.Break))], candidate: null, OldBaseLine(id));
            }

            return;
        }

        var note = FieldsOf(line);
        if (todo is null)
        {
            AddUnknown(line, id, note);
        }
        else if (todo.Removed)
        {
            RemovedInSindri(line, id, todo, note);
        }
        else
        {
            Live(line, id, todo, note);
        }
    }

    private void AddUnknown(NoteTodoLine line, TodoId id, NoteFields note)
    {
        if (!TryAdopt())
        {
            return;
        }

        var text = LineOf(line, id, note);
        if (!string.Equals(text, line.Raw, StringComparison.Ordinal))
        {
            _edits.Add(NoteLineEdit.Replace(line.Number, text));
        }

        AddSlot(id, [new NoteAdd(id, note.Text, note.Due, note.Links, note.Done, note.DoneOn)], candidate: null, text);
    }

    private void NewLine(NoteTodoLine line)
    {
        if (line.Break != NoteBreak.None || !TryAdopt())
        {
            return;
        }

        var note = FieldsOf(line);
        var id = _options.Minter.Mint(line.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + line.Raw);
        var text = string.Equals(note.Text, line.Text, StringComparison.Ordinal) ? WithId(line, id) : Render(id, note);
        _edits.Add(NoteLineEdit.Replace(line.Number, text));
        AddSlot(id, [new NoteAdopt(id, note.Text, note.Due, note.Links, note.Done, note.DoneOn, line.Number)], candidate: null, text);
    }

    private void Live(NoteTodoLine line, TodoId id, SindriTodo todo, NoteFields note)
    {
        var sindri = NoteFields.Of(todo);
        var noteLine = LineOf(line, id, note);
        if (_options.OpenConflicts.TryGetValue(id, out var open))
        {
            // D11: nothing about the todo syncs until the owner picks. The note's side or Sindri's moving again re-asks; the two agreeing closes it.
            if (note.Is(open))
            {
                KeepBase(id);
            }
            else
            {
                Settle(id, note.SameAs(sindri), sindri.ToSide(), note, noteLine);
            }

            return;
        }

        var from = BaseFields(id);
        if (from is null)
        {
            Settle(id, note.SameAs(sindri), sindri.ToSide(), note, noteLine);
        }
        else if (note.SameAs(from) && sindri.SameAs(from))
        {
            SetBase(id, noteLine);
        }
        else if (sindri.SameAs(from))
        {
            FromNote(line, id, from, note, noteLine);
        }
        else if (note.SameAs(from))
        {
            // why: the owner's own text stays as typed until Sindri changes the text (an adopted `[[link]]` is not escaped by a tick)
            var rendered = string.Equals(todo.Text, line.Text, StringComparison.Ordinal)
                ? NoteRenderer.RenderLine(todo, NoteTodoLineReader.RawTextOf(line.Raw))
                : NoteRenderer.RenderLine(todo);
            _edits.Add(NoteLineEdit.Replace(line.Number, rendered));
            SetBase(id, rendered);
        }
        else
        {
            Settle(id, note.SameAs(sindri), sindri.ToSide(), note, noteLine);
        }
    }

    private void Settle(TodoId id, bool agree, NoteSide sindri, NoteFields note, string noteLine)
    {
        if (agree && _options.OpenConflicts.ContainsKey(id))
        {
            AddSlot(id, [new NoteConflictSettled(id)], candidate: null, noteLine);
        }
        else if (agree)
        {
            SetBase(id, noteLine);
        }
        else
        {
            AddSlot(id, [new NoteConflict(id, sindri, note.ToSide())], candidate: null, OldBaseLine(id));
        }
    }

    private void FromNote(NoteTodoLine line, TodoId id, NoteFields from, NoteFields note, string noteLine)
    {
        HeldChange? candidate = null;
        if (!note.Done && from.Done)
        {
            candidate = new HeldChange(id, HeldKind.Reopened, line.Raw);
        }
        else if (!note.Done && note.FieldsDiffer(from))
        {
            candidate = new HeldChange(id, HeldKind.Edited, line.Raw);
        }

        AddSlot(id, note.IntentsFrom(id, from), candidate, noteLine);
    }

    private void RemovedInSindri(NoteTodoLine line, TodoId id, SindriTodo todo, NoteFields note)
    {
        var noteLine = LineOf(line, id, note);
        var bySindri = todo.RemovedBy == NoteOrigin.Sindri;
        if (bySindri && _base.ContainsKey(id))
        {
            RemovedByAppNoteUntouched(line, id, note);
        }
        else if (bySindri && string.Equals(todo.LastLineHash, NoteLineHash.Of(noteLine), StringComparison.Ordinal))
        {
            // A stale copy of a line Sindri removed (an old note restored, a sync lagging): dropped again, once there is a base to drop it against.
            if (_hasBase)
            {
                _edits.Add(NoteLineEdit.Remove(line.Number));
            }

            SetBase(id, _hasBase ? null : noteLine);
        }
        else
        {
            AddSlot(id, new[] { new NoteRestore(id) }.Concat(note.IntentsFrom(id, NoteFields.Of(todo))), candidate: null, noteLine);
        }
    }

    private void RemovedByAppNoteUntouched(NoteTodoLine line, TodoId id, NoteFields note)
    {
        if (_options.OpenConflicts.TryGetValue(id, out var open))
        {
            if (note.Is(open))
            {
                KeepBase(id);
            }
            else
            {
                AddSlot(id, [new NoteConflict(id, _removedSide, note.ToSide())], candidate: null, OldBaseLine(id));
            }
        }
        else if (note.SameAs(BaseFields(id)!)) // why: the caller checked the base has the id
        {
            _edits.Add(NoteLineEdit.Remove(line.Number));
            SetBase(id, line: null);
        }
        else
        {
            AddSlot(id, [new NoteConflict(id, _removedSide, note.ToSide())], candidate: null, OldBaseLine(id));
        }
    }

    private void MissingLine(TodoId id)
    {
        if (_options.LeaveAlone.Contains(id))
        {
            KeepBase(id);
            return;
        }

        if (!_sindri.TryGetValue(id, out var todo) || todo.Removed)
        {
            if (_options.OpenConflicts.ContainsKey(id))
            {
                AddSlot(id, [new NoteConflictSettled(id)], candidate: null, newBaseLine: null);
            }
            else
            {
                SetBase(id, line: null);
            }

            return;
        }

        var from = BaseFields(id)!; // why: the id came from the base
        var sindri = NoteFields.Of(todo);
        if (_options.OpenConflicts.TryGetValue(id, out var open))
        {
            if (open.Removed)
            {
                KeepBase(id);
            }
            else
            {
                AddSlot(id, [new NoteConflict(id, sindri.ToSide(), _removedSide)], candidate: null, OldBaseLine(id));
            }
        }
        else if (sindri.SameAs(from))
        {
            AddSlot(id, [new NoteRemove(id)], from.Done ? null : new HeldChange(id, HeldKind.Removed, NoteLine: null), newBaseLine: null);
        }
        else
        {
            AddSlot(id, [new NoteConflict(id, sindri.ToSide(), _removedSide)], candidate: null, OldBaseLine(id));
        }
    }

    private void AppendFromSindri()
    {
        foreach (var todo in _sindriList)
        {
            if (todo.Removed || _options.LeaveAlone.Contains(todo.Id) || _noteFirst.ContainsKey(todo.Id) || _base.ContainsKey(todo.Id) || _newOrder.Contains(todo.Id))
            {
                continue;
            }

            var line = NoteRenderer.RenderLine(todo);
            _edits.Add(NoteLineEdit.Append(line));
            SetBase(todo.Id, line);
        }
    }

    private bool TryAdopt()
    {
        if (_adopted >= _options.MaxAdoptions)
        {
            _deferred++;
            return false;
        }

        _adopted++;
        return true;
    }

    /// <summary>The base fields of an id: the line a pick was resolved against when there is one, else the saved base line.</summary>
    private NoteFields? BaseFields(TodoId id)
    {
        if (_options.PickBases.TryGetValue(id, out var picked) && NoteTodoLineReader.IsTaskLine(picked))
        {
            return FieldsOf(NoteTodoLineReader.Read(0, picked));
        }

        return _base.TryGetValue(id, out var entry) ? entry.Fields : null;
    }

    private string? OldBaseLine(TodoId id) => _base.TryGetValue(id, out var entry) ? entry.Line : null;

    /// <summary>The line to save as base for a note line: the note's own text when the scrubber left it alone, else the line written from the scrubbed fields.</summary>
    private static string LineOf(NoteTodoLine line, TodoId id, NoteFields note) =>
        string.Equals(note.Text, line.Text, StringComparison.Ordinal) ? line.Raw : Render(id, note);

    private static string Render(TodoId id, NoteFields note) =>
        NoteRenderer.RenderLine(new SindriTodo(id, note.Text, note.Due, note.Links, note.Done, note.DoneOn));

    /// <summary>The owner's line with its id (a bad or repeated one, or none) replaced by the minted one, the rest kept as typed.</summary>
    private static string WithId(NoteTodoLine line, TodoId id)
    {
        var raw = line.Raw.TrimEnd();
        if (line.Id is not null || line.RejectedBlock is not null)
        {
            raw = raw[..raw.AsSpan().LastIndexOfAny(" \t")].TrimEnd();
        }

        return raw + " " + NoteTodoLineReader.IdMarker + id.Value["td_".Length..];
    }
}
