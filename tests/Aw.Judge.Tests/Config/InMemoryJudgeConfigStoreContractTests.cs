using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Config;

[Lane(TestLane.Unit)]
public sealed class InMemoryJudgeConfigStoreContractTests : JudgeConfigStoreContract
{
    protected override ValueTask<IJudgeConfigStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IJudgeConfigStore>(new InMemoryJudgeConfigStore());

    protected override ValueTask<IJudgeConfigStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IJudgeConfigStore>(new InMemoryJudgeConfigStore { Unavailable = true });
}
