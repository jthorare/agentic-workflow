using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>Making a sandbox refusal and reading its reason and source back (sandbox spec §6; engine asks, #352).</summary>
public sealed class SandboxRefusalTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_codes_every_reason_round_trips()
    {
        foreach (var (code, reason) in SandboxReasons.All)
        {
            var definition = SandboxCodes.All.Single(c => string.Equals(c.Error.Code, code, StringComparison.Ordinal)).Error;
            var spelled = string.Equals(reason, SandboxReasons.HttpStatus, StringComparison.Ordinal) ? "http-503" : reason;

            var error = SandboxRefusal.For(definition, spelled);

            Assert.Equal(spelled, SandboxRefusal.ReasonOf(error));
            Assert.Null(SandboxRefusal.SourceOf(error));
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ModelLoginSource.OwnerCopy)]
    [InlineData(ModelLoginSource.SeparateLogin)]
    public void The_source_round_trips(ModelLoginSource source) =>
        Assert.Equal(source, SandboxRefusal.SourceOf(SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, source: source)));

    [Fact, Trait("Lane", "unit")]
    public void A_non_sandbox_error_has_no_reason_or_source()
    {
        var error = SindriErrors.LaunchViewerFailed.With("no window");

        Assert.Null(SandboxRefusal.ReasonOf(error));
        Assert.Null(SandboxRefusal.SourceOf(error));
    }

    [Fact, Trait("Lane", "unit")]
    public void Named_details_become_extensions_the_copy_can_fill()
    {
        var error = SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions,
            details: new Dictionary<string, string>(StringComparer.Ordinal) { ["cap"] = "3", ["running"] = "3" });

        Assert.Equal("3", error.Extensions!.Single(e => e.Key == "cap").Value.GetString());
        Assert.Equal("3", error.Extensions!.Single(e => e.Key == "running").Value.GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_template_http_reason_is_not_a_real_reason() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, SandboxReasons.HttpStatus));
}
