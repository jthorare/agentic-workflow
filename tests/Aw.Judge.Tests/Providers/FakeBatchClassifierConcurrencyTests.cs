using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>The scan sends its batches in parallel, so the fake records every one of them.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeBatchClassifierConcurrencyTests
{
    [Fact]
    public async Task Every_batch_asked_at_the_same_time_is_recorded()
    {
        const int calls = 20_000;
        var fake = new FakeBatchClassifier(static _ => new ProviderUnavailable("unavailable"));
        var request = new BatchRequest(State: null, Questions: [], Budget: TimeSpan.FromSeconds(1));

        await Parallel.ForAsync(0, calls, new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = TestContext.Current.CancellationToken }, async (index, token) =>
            _ = await fake.Classify(request, providers: null, allowed: null, token));

        Assert.Equal(calls, fake.Asked.Count);
    }
}
