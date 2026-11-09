using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Infrastructure.Todos;

#pragma warning disable MA0182 // until the todo sync registers it

/// <summary>The note sync's files on disk (todo spec §4.5): stub, not written yet.</summary>
internal sealed class FileTodoNoteFiles(ISindriHome home, TimeProvider clock) : ITodoNoteFiles
{
    private readonly string _folder = Path.Combine(home.Path, "todo");
    private readonly TimeProvider _clock = clock;

    public NoteRead Read(string note) => throw new NotSupportedException(_folder + _clock.GetUtcNow().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public ValueTask<(NoteRead Read, bool Settled)> ReadQuiet(string path, TimeSpan quiet, TimeSpan max, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void WriteIntent(string hash) => throw new NotSupportedException();

    public string? ReadIntent() => throw new NotSupportedException();

    public void ClearIntent() => throw new NotSupportedException();

    public NoteSwap Swap(string note, string text) => throw new NotSupportedException();

    public string? Leftover(string note) => throw new NotSupportedException();

    public void Delete(string path) => throw new NotSupportedException();

    public string Keep(string path, DateTimeOffset at) => throw new NotSupportedException();

    public string SaveBase(NoteBase noteBase) => throw new NotSupportedException();

    public NoteBase? LoadBase(string hash) => throw new NotSupportedException();

    public void DeleteBases(string? keep) => throw new NotSupportedException();
}

#pragma warning restore MA0182
