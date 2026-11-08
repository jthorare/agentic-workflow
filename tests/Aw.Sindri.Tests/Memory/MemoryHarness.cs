using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ExpireMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;
using Aw.Sindri.Testing.Memory;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The in-memory ports the memory slices need, and the slices built on them.</summary>
internal sealed class MemoryHarness
{
    public MemoryHarness()
    {
        Callers = new AsyncLocalCallerContextAccessor(Ids);
    }

    public InMemoryMemoryStore Store { get; } = new();

    public FakeMemoryEmbedder Embedder { get; } = new();

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public SequentialIdGenerator Ids { get; } = new();

    public ITextScrubber Scrubber { get; set; } = new NoScrub();

    public ICallerContextAccessor Callers { get; }

    public CreateMemoryService Create() => new(Store, Scrubber, Clock, Callers);

    public ListMemoriesService List() => new(Store);

    public SearchMemoryService Search() => new(Store, Embedder, Clock);

    public WriteMemoryFactService Write() => new(Store, Embedder, Scrubber, Clock, Ids);

    public ForgetMemoryFactService Forget() => new(Store, Clock);

    public ExpireMemoryService Expire() => new(Store, Embedder, Clock);

    public async Task Memory(string name = "team-notes") => _ = await Create().Handle(new CreateMemoryRequest(name, Description: null), TestContext.Current.CancellationToken);

    public static WriteMemoryFactRequest Fact(string text, params string[] tags) => new("team-notes", text, tags, ExpiresAt: null, Provenance: null);

    private sealed class NoScrub : ITextScrubber
    {
        public string Scrub(string text) => text;
    }
}
