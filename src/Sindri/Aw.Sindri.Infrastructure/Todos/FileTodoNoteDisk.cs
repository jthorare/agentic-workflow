using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Infrastructure.Todos;

/// <summary>Stub: the red commit of the note check's disk side.</summary>
internal sealed class FileTodoNoteDisk : ITodoNoteDisk
{
    public NoteFileFacts Inspect(string vaultRoot, string note) => throw new NotSupportedException();

    public NoteFolderFacts Probe(string vaultRoot, string note) => throw new NotSupportedException();
}
