using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Core;

public sealed class SourceDescriptorTests
{
    private static readonly SourceDescriptor _linear = new(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    [Fact, Trait("Lane", "unit")]
    public void A_source_is_local_with_no_limits_unless_it_says_otherwise()
    {
        Assert.Equal(SourcePlacement.Local, _linear.Placement);
        Assert.Null(_linear.Limits);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_shared_source_with_limits_keeps_what_it_is()
    {
        var shared = _linear with { Placement = SourcePlacement.Shared, Limits = new SourceLimits(Requests: 60, Window: TimeSpan.FromMinutes(1), MaxConcurrency: 4) };

        Assert.Equal((_linear.Id, _linear.Kind, _linear.Freshness), (shared.Id, shared.Kind, shared.Freshness));
        Assert.Equal(SourcePlacement.Shared, shared.Placement);
        Assert.True(shared.Limits.IsWellFormed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null, null, false)]
    [InlineData(60, 60, null, true)]
    [InlineData(null, null, 4, true)]
    [InlineData(60, null, null, false)]
    [InlineData(null, 60, null, false)]
    [InlineData(0, 60, null, false)]
    [InlineData(60, 0, null, false)]
    [InlineData(null, null, 0, false)]
    public void Limits_are_positive_and_a_request_count_comes_with_its_window(int? requests, int? windowSeconds, int? concurrency, bool wellFormed) =>
        Assert.Equal(wellFormed, new SourceLimits(requests, windowSeconds is { } s ? TimeSpan.FromSeconds(s) : null, concurrency).IsWellFormed);
}
