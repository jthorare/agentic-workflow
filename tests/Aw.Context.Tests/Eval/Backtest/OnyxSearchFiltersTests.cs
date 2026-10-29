using Aw.Context.Infrastructure.Onyx;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class OnyxSearchFiltersTests
{
    [Fact, Trait("Lane", "unit")]
    public void OnyxSearchFilters_send_time_cutoff_only_when_a_window_start_is_named()
    {
        var empty = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(new OnyxAdminSearchRequest("q", new OnyxSearchFilters()), OnyxWire.Options));
        var windowed = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(new OnyxAdminSearchRequest("q", new OnyxSearchFilters(new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero))), OnyxWire.Options));

        Assert.DoesNotContain("time_cutoff", empty, StringComparison.Ordinal);
        Assert.Contains("\"time_cutoff\":\"2026-07-02T00:00:00", windowed, StringComparison.Ordinal);
    }
}
