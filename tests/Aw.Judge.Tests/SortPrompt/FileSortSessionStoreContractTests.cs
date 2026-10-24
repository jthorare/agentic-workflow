using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.PromptSort;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

[Lane(TestLane.Integration)]
public sealed class FileSortSessionStoreContractTests : SortSessionStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<ISortSessionStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ISortSessionStore>(new FileSortSessionStore(Path.Combine(_state.Path, "judge", "sessions")));

    // The judge directory's parent is a regular file, so the sessions directory can't be created.
    protected override async ValueTask<ISortSessionStore> CreateUnavailable(CancellationToken cancellationToken)
    {
        var file = Path.Combine(_state.Path, "not-a-directory");
        await File.WriteAllTextAsync(file, "x", cancellationToken);
        return new FileSortSessionStore(Path.Combine(file, "judge", "sessions"));
    }

    public void Dispose() => _state.Dispose();
}
