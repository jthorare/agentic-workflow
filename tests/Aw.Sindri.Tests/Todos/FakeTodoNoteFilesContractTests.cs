using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The in-memory note files honour the contract.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeTodoNoteFilesContractTests : TodoNoteFilesContract
{
    private readonly FakeTodoNoteFiles _files = new();

    protected override string NotePath => "/vault/Todo.md";

    protected override ITodoNoteFiles Create() => _files;

    protected override void Put(string path, string text) => _files.Files[path] = text;

    protected override string? Text(string path) => _files.Files.TryGetValue(path, out var text) ? text : null;
}
