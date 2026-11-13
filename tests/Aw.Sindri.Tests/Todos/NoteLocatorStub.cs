using Aw.Kernel;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Tests.Todos;

/// <summary>A note locator whose place a test sets; none by default (no key).</summary>
internal sealed class NoteLocatorStub : INoteLocator
{
    public NotePlace? Place { get; set; }

    /// <summary>When set, the locate fails with it (no profile, or a vault that can't be resolved).</summary>
    public Result<NotePlace?>? Fails { get; set; }

    public ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken) => ValueTask.FromResult(Fails ?? Results.Success(Place));
}
