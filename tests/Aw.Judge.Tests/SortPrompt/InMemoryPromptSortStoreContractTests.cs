using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

[Lane(TestLane.Unit)]
public sealed class InMemoryPromptSortStoreContractTests : PromptSortStoreContract
{
    protected override ValueTask<IPromptSortStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IPromptSortStore>(new InMemoryPromptSortStore());

    protected override ValueTask<IPromptSortStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IPromptSortStore>(new InMemoryPromptSortStore { Unavailable = true });
}
