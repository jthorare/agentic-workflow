using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

[Lane(TestLane.Unit)]
public sealed class ToolkitCallLogTests
{
    // spelled in two parts so no secret scanner mistakes the test for a leak
    private const string Token = "ghp" + "_0123456789abcdefghijklmnopqrstuvwxyzAB";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_call_is_logged_with_caller_duration_and_a_scrubbed_capped_payload()
    {
        var store = new InMemoryToolkitCalls();
        var log = new ToolkitCallLog(store, new RedactingScrubber(Redactor.Default));
        var at = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var big = Token + new string('x', ToolkitCallLog.MaxPayloadChars * 2);

        var recorded = await log.Record(new ToolkitCallEntry("notes", "search", "session:abc", $"{{\"auth\":\"{Token}\"}}", big, $"failed with {Token}", TimeSpan.FromMilliseconds(250), at), Cancel);

        Assert.True(recorded.IsSuccess);
        var row = Assert.Single(store.All);
        Assert.Equal(("notes", "search", "session:abc", 250L, at), (row.Toolkit, row.Tool, row.Caller, row.DurationMs, row.At));
        Assert.DoesNotContain(Token, row.Input, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, row.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, row.Error, StringComparison.Ordinal);
        Assert.True(row.Truncated);
        Assert.Equal(ToolkitCallLog.MaxPayloadChars, row.Output?.Length);
    }

    [Fact]
    public async Task A_call_under_the_cap_is_kept_whole_and_not_marked_truncated()
    {
        var store = new InMemoryToolkitCalls();
        var log = new ToolkitCallLog(store, new RedactingScrubber(Redactor.Default));

        _ = await log.Record(new ToolkitCallEntry("notes", "search", "human", "{}", "ok", Error: null, TimeSpan.Zero, DateTimeOffset.UnixEpoch), Cancel);

        var row = Assert.Single(store.All);
        Assert.False(row.Truncated);
        Assert.Equal(("{}", "ok", null), (row.Input, row.Output, row.Error));
    }

    [Fact]
    public void The_cap_never_splits_a_surrogate_pair()
    {
        var text = new string('a', ToolkitCallLog.MaxPayloadChars - 1) + "😀";

        var capped = ToolkitCallLog.Cap(text, out var truncated);

        Assert.True(truncated);
        Assert.Equal(ToolkitCallLog.MaxPayloadChars - 1, capped?.Length);
    }
}
