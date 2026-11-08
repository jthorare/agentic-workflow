using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class MemoryReadsAndCreateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateMemory_makes_a_store_and_a_second_one_with_the_name_is_SND_MEM_002()
    {
        var h = new MemoryHarness();

        var first = await h.Create().Handle(new CreateMemoryRequest("team-notes", "Shared notes"), Token);
        var second = await h.Create().Handle(new CreateMemoryRequest("team-notes", null), Token);

        Assert.Equal("team-notes", first.Value.Store.Name);
        Assert.Equal(SindriErrors.MemoryNameTaken.Code, second.Error.Code);
    }

    [Fact]
    public async Task CreateMemory_refuses_a_bad_name()
    {
        var h = new MemoryHarness();

        var result = await h.Create().Handle(new CreateMemoryRequest("Team Notes", null), Token);

        Assert.Equal(SindriErrors.MemoryNameInvalid.Code, result.Error.Code);
        Assert.Empty((await h.Store.List(Token)).Value);
    }

    [Fact]
    public async Task ListMemories_shows_each_store_with_its_fact_count_sorted_by_name()
    {
        var h = new MemoryHarness();
        await h.Memory("zeta");
        await h.Memory("alpha");
        _ = await h.Store.Add(MemorySamples.Fact(memory: MemorySamples.Name("alpha")), Token);

        var listed = await h.List().Handle(new ListMemoriesRequest(), Token);

        Assert.Equal(["alpha", "zeta"], listed.Value.Memories.Select(static m => m.Name));
        Assert.Equal([1, 0], listed.Value.Memories.Select(static m => m.Facts));
    }
}
