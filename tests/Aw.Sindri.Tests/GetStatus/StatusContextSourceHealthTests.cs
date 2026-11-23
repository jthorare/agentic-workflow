using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// A context source the server flags as needing a look is a health problem (#4010): the pill and the System headline read the health word, so a
/// flagged Context index card under a "Healthy" pill was the page saying two opposite things.
/// </summary>
public sealed class StatusContextSourceHealthTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task A_flagged_context_source_makes_health_degraded_with_one_problem_line_and_its_code()
    {
        H.Profiles.Live = SliceHarness.Profile("5s");
        _ = await H.HoldLease();
        _v.Publish(stalledAt: null);
        var sources = new FixedSourceHealth(new HealthProblem("SND-CONTEXT-071", "linear: Linear refused the key (401), retrying", "the linear source needs a look"));
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, _v.Progress, new HealthServiceRows(_v.Views, [], sources: sources), new InMemorySessionSnapshot());

        var status = (await service.Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        Assert.Equal(["SND-CONTEXT-071"], status.Health.Problems.Select(p => p.Code), StringComparer.Ordinal);
        Assert.Equal("Sindri is degraded: the linear source needs a look.", status.Health.Sentence);
        Assert.Equal(H.Profiles.Live.Value.Hash, sources.AskedWith?.Hash);
    }

    private sealed class FixedSourceHealth(params HealthProblem[] problems) : IContextSourceHealth
    {
        public LoadedProfile? AskedWith { get; private set; }

        public ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken)
        {
            AskedWith = approved;
            return ValueTask.FromResult<IReadOnlyList<HealthProblem>>(problems);
        }
    }
}
