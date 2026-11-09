using Aw.Sindri.Core.Todos.Note;
using Aw.Kernel;

namespace Aw.Sindri.Tests.Todos;

/// <summary>A note locator whose place a test sets; none by default (no key).</summary>
internal sealed class NoteLocatorStub : INoteLocator
{
    public NotePlace? Place { get; set; }

    public ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Place));
}
