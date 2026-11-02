using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.RecordLimitHit;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.RecordLimitHit;

/// <summary>'POST /launches/{launch}/limit-hit' (sandbox spec §3.3): proven by the launch secret, recorded once per launch and kind.</summary>
[Lane(TestLane.Unit)]
public sealed class RecordLimitHitTests
{
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RecordLimitHitRequest Request(string proof = FakeLaunchFiles.DefaultProof, LimitHitKind kind = LimitHitKind.Pids, long peak = 2048) =>
        new(A.FirstLaunchId, new LaunchSecret(proof), kind, peak);

    private RecordLimitHitService Service() => new(_s.Events());

    [Fact]
    public async Task A_limit_hit_is_recorded_with_its_kind_and_peak()
    {
        await _s.Started();

        var result = await Service().Handle(Request(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SandboxLimitHit>(recorded).Value;
        Expect.Equal((EventTypes.SandboxLimitHit, $"sandbox.limit-hit:{A.FirstLaunchId.Value}:pids:1", A.FirstItemId.Value), (recorded.Envelope.Type, recorded.IdempotencyKey, recorded.Item));
        Expect.Equal((A.FirstLaunchId, LimitHitKind.Pids, 2048L), (payload.Launch, payload.Kind, payload.Peak));
    }

    [Fact]
    public async Task A_repeat_is_a_success_and_records_nothing_more()
    {
        await _s.Started();
        _ = await Service().Handle(Request(), Token);

        var again = await Service().Handle(Request(peak: 4000), Token);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        _ = Assert.Single(_s.Views.Slice.Store.Events);
    }

    [Fact]
    public async Task Another_kind_is_its_own_event()
    {
        await _s.Started();
        _ = await Service().Handle(Request(), Token);

        _ = await Service().Handle(Request(kind: LimitHitKind.Memory), Token);

        Expect.Equal(2, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task A_wrong_secret_is_SND_AUTH_003_and_records_nothing()
    {
        await _s.Started();

        var result = await Service().Handle(Request(proof: "not-the-proof"), Token);

        Expect.True(result.IsError && string.Equals(result.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }
}
