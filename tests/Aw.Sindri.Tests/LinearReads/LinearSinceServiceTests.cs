using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.GetLinearSince;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearReads;

/// <summary>GET /sindri/linear/since (Linear hub spec 2026-11-04 §3, §4.3): owner only; the marker it returns is the newest change's instant and reads back as nothing newer.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearSinceServiceTests
{
    private static readonly DateTimeOffset _first = new(2025, 12, 31, 22, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _second = new(2025, 12, 31, 23, 40, 0, TimeSpan.Zero);

    private readonly SliceHarness _slice = new();
    private readonly FakeLinearSideFacts _facts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLinearSinceService Service() => new(_facts, _slice.Callers);

    private static LinearSinceItem Item(string kind, string key, DateTimeOffset at) => new(kind, key, "Retry budget", new LinearPerson("u2", "Sam Lee"), at, From: null, To: null);

    private IDisposable Owner() => _slice.Callers.Enter(A.Caller().Human().Build());

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_read()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new GetLinearSinceRequest(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_facts.AskedAfter);
    }

    [Fact]
    public async Task Contexts_session_refusal_SND_CONTEXT_020_is_SND_AUTH_002()
    {
        using var entered = Owner();
        _ = _facts.Failing(ContextErrors.SessionsDisabled.With("what changed is the owner's"));

        var result = await Service().Handle(new GetLinearSinceRequest(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(GetLinearSinceService.Refusal, result.Error.Message);
    }

    [Fact]
    public async Task No_marker_reads_from_the_start_of_what_is_stored()
    {
        using var entered = Owner();
        _ = _facts.WithItems(Item("assigned", "ENG-31", _first), Item("state", "ENG-12", _second));

        var result = await Service().Handle(new GetLinearSinceRequest(), Token);

        Expect.Equal(DateTimeOffset.MinValue, _facts.AskedAfter.Single());
        Expect.Equal(2, result.Value.Count);
        Expect.Sequence(["ENG-31", "ENG-12"], [.. result.Value.Items.Select(static i => i.Key)]);
        Expect.Equal(LinearSinceMarkers.Format(_second), result.Value.Marker);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("lin_3f")]
    [InlineData("lin_000000000000003F")]
    [InlineData("lin_00000000000000zz")]
    [InlineData("lin_ffffffffffffffff")]
    [InlineData("lin_00000000000000003f")]
    public async Task A_marker_this_route_never_gave_is_SND_API_013(string after)
    {
        using var entered = Owner();

        var result = await Service().Handle(new GetLinearSinceRequest(after), Token);

        Expect.Equal(SindriErrors.InvalidQuery.Code, result.Error.Code);
        Assert.Empty(_facts.AskedAfter);
    }

    [Fact]
    public async Task The_marker_the_route_returns_reads_back_as_no_changes()
    {
        using var entered = Owner();
        _ = _facts.WithItems(Item("assigned", "ENG-31", _first), Item("state", "ENG-12", _second));
        var first = await Service().Handle(new GetLinearSinceRequest(), Token);

        var again = await Service().Handle(new GetLinearSinceRequest(first.Value.Marker), Token);

        Expect.Equal(_second, _facts.AskedAfter[^1]);
        Expect.Equal(0, again.Value.Count);
        Assert.Empty(again.Value.Items);
        Expect.Equal(first.Value.Marker, again.Value.Marker);
    }

    [Fact]
    public async Task No_viewer_yet_is_a_200_with_no_changes_and_the_marker_as_asked()
    {
        using var entered = Owner();
        _ = _facts.WithoutViewer();
        var asked = LinearSinceMarkers.Format(_first);

        var result = await Service().Handle(new GetLinearSinceRequest(asked), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(0, result.Value.Count);
        Assert.Empty(result.Value.Items);
        Expect.Equal(asked, result.Value.Marker);
    }

    [Fact]
    public void A_marker_is_lin_and_the_utc_ticks_in_16_lowercase_hex_digits()
    {
        var marker = LinearSinceMarkers.Format(new DateTimeOffset(63, TimeSpan.Zero));

        Expect.Equal("lin_000000000000003f", marker);
        Assert.True(LinearSinceMarkers.TryParse(marker, out var back));
        Expect.Equal(new DateTimeOffset(63, TimeSpan.Zero), back);
    }
}
