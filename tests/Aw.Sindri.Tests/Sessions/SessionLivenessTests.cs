using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A session is live only when its process is the same process (collector spec CO5).</summary>
public sealed class SessionLivenessTests
{
    [Fact, Trait("Lane", "unit")]
    public void SessionLiveness_rejects_a_recycled_pid() =>
        Assert.False(SessionLiveness.Matches(procStart: "1100", actualTicks: 9000));

    [Fact, Trait("Lane", "unit")]
    public void SessionLiveness_reads_procStart_as_a_string()
    {
        Assert.True(SessionLiveness.Matches(procStart: "13279367", actualTicks: 13279367));
        Assert.False(SessionLiveness.Matches(procStart: null, actualTicks: 1));
        Assert.False(SessionLiveness.Matches(procStart: "not a number", actualTicks: 1));
        Assert.False(SessionLiveness.Matches(procStart: "", actualTicks: 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void SessionLiveness_reads_a_macOS_procStart_as_ps_lstart_text_in_UTC()
    {
        var started = new DateTimeOffset(2026, 10, 21, 16, 22, 11, TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.True(SessionLiveness.Matches(procStart: "Wed Oct 21 16:22:11 2026", started));
        Assert.False(SessionLiveness.Matches(procStart: "Wed Oct 21 16:22:11 2026", started + 1));
        Assert.False(SessionLiveness.Matches(procStart: "Wed Oct 21 16:22:12 2026", started));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Wed Oct 21 16:22:11 2026", 2026, 10, 21, 16, 22, 11)]
    [InlineData("Sun Jan  4 00:00:01 2026", 2026, 1, 4, 0, 0, 1)]
    public void PsStartTime_reads_lstart_text_as_epoch_seconds(string text, int year, int month, int day, int hour, int minute, int second) =>
        Assert.Equal(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero).ToUnixTimeSeconds(), PsStartTime.ToEpochSeconds(text));

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("13279367")]
    [InlineData("Wed Foo 21 16:22:11 2026")]
    [InlineData("Wed Oct 21 16:22 2026")]
    public void PsStartTime_is_null_for_anything_else(string? text) => Assert.Null(PsStartTime.ToEpochSeconds(text));
}
