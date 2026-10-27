using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Carried from #352 run 2: 'reason' and 'source' belong to the refusal, not to its named details.</summary>
public sealed class SandboxRefusalDetailsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("reason")]
    [InlineData("source")]
    public void Details_may_not_use_the_reserved_keys(string key)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal) { [key] = "x" };

        _ = Assert.Throws<ArgumentException>(() => SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions, details: details));
    }

    [Fact, Trait("Lane", "unit")]
    public void Named_details_become_extensions_next_to_the_reason()
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["cap"] = "3" };

        var error = SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions, details: details);

        Assert.Equal("sessions", SandboxRefusal.ReasonOf(error));
        Assert.Equal("3", error.Extensions!.Single(e => string.Equals(e.Key, "cap", StringComparison.Ordinal)).Value.GetString());
    }
}
