using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The ledger end of the denial counter: 'sandbox.denied' once per key (sandbox spec §8, §9).</summary>
[Lane(TestLane.Integration)]
public sealed class LedgerDenialsTests
{
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_denial_is_appended_once_per_key_and_a_repeat_is_a_no_op()
    {
        await _s.Started();
        var denied = new SandboxDenied(A.FirstLaunchId, DenialKind.Network, "evil.example", "no-group", 10);

        var first = await _s.Events().Append(denied, Token);
        var again = await _s.Events().Append(denied, Token);
        var next = await _s.Events().Append(denied with { Count = 100 }, Token);

        Assert.True(first.IsSuccess && again.IsSuccess && next.IsSuccess);
        Assert.Equal(2, _s.Views.Slice.Store.Events.Count);
        var recorded = _s.Views.Slice.Store.Events[0];
        Assert.Equal(EventTypes.SandboxDenied, recorded.Envelope.Type);
        Assert.StartsWith($"sandbox.denied:{A.FirstLaunchId.Value}:network:", recorded.IdempotencyKey, StringComparison.Ordinal);
        Assert.EndsWith(":10", recorded.IdempotencyKey, StringComparison.Ordinal);
    }
}
