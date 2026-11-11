using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Infrastructure.Todos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The note files on disk honour the same contract as the fake.</summary>
[Lane(TestLane.Integration)]
public sealed class FileTodoNoteFilesContractTests : TodoNoteFilesContract, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-todo-contract-").FullName;

    public FileTodoNoteFilesContractTests()
    {
        _ = Directory.CreateDirectory(Path.Combine(_root, "vault"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    protected override string NotePath => Path.Combine(_root, "vault", "Todo.md");

    protected override ITodoNoteFiles Create() => new FileTodoNoteFiles(new FakeSindriHome(Path.Combine(_root, "home")), TimeProvider.System);

    protected override void Put(string path, string text) => File.WriteAllText(path, text);

    protected override string? Text(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
