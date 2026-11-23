using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>'sindri restart [--wait] [--timeout D]' (first-run spec §5.1): --wait waits 30 minutes unless --timeout names s, m or h; anything else is SND-CLI-001.</summary>
public sealed class RestartArgumentsTests
{
    [Fact, Trait("Lane", "unit")]
    public void No_flags_refuse_at_once_and_wait_defaults_to_thirty_minutes()
    {
        Assert.Equal(new RestartArguments(Wait: false, TimeSpan.FromMinutes(30)), RestartArguments.Parse([]).Value);
        Assert.Equal(new RestartArguments(Wait: true, TimeSpan.FromMinutes(30)), RestartArguments.Parse(["--wait"]).Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("90s", 90)]
    [InlineData("45", 45)]
    [InlineData("10m", 600)]
    [InlineData("2h", 7200)]
    public void Timeout_takes_seconds_minutes_or_hours(string text, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), RestartArguments.Parse(["--wait", "--timeout", text]).Value.Timeout);

    [Theory, Trait("Lane", "unit")]
    [InlineData("--now")]
    [InlineData("--timeout")]
    [InlineData("--timeout", "5d")]
    [InlineData("--timeout", "0m")]
    public void Other_flags_are_refused(params string[] args) =>
        Assert.Equal(SindriErrors.UnknownVerb.Code, RestartArguments.Parse(args).Error.Code);
}
