using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'launch:' and 'agents:' blocks (UI E-4, screens E-28, launcher L-11 and L-14).</summary>
public sealed class LaunchAgentKeysTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Profile_without_launch_or_agents_keeps_its_hash()
    {
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\n");
        var plain = (await _files.Source.Load(Token)).Value;

        Assert.Null(plain.Profile.Launch);
        Assert.Null(plain.Profile.Agents);
        var canonical = System.Text.Json.JsonSerializer.Serialize(plain.Profile, ProfileFiles.Options);
        Assert.DoesNotContain("launch", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("agents", canonical, StringComparison.Ordinal);

        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\nlaunch:\n  default: codex\n");
        Assert.NotEqual(plain.Hash, (await _files.Source.Load(Token)).Value.Hash);
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\n");
        Assert.Equal(plain.Hash, (await _files.Source.Load(Token)).Value.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_launch_and_agents_with_one_hash_or_a_list()
    {
        _files.Write(Base + "launch:\n  default: claude-code\n  confirmWindow: 90m\n  repos:\n    web: { default: aider }\n"
            + "agents:\n  softLimit: 0.9\n  claude-code: { dailyBudget: 12.5 }\n  aider: { stepArgvHash: abc123 }\n  codex: { stepArgvHash: [h1, h2] }\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        Assert.Equal("claude-code", profile.Launch!.Default!.Value.Value);
        Assert.Equal("90m", profile.Launch.ConfirmWindow);
        Assert.Equal("aider", profile.Launch.Repos!["web"].Default!.Value.Value);
        Assert.Equal(0.9m, profile.Agents!.SoftLimit);
        Assert.Equal(12.5m, profile.Agents.Targets!["claude-code"].DailyBudget);
        Assert.Equal(["abc123"], profile.Agents.Targets["aider"].StepArgvHash);
        Assert.Equal(["h1", "h2"], profile.Agents.Targets["codex"].StepArgvHash);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("launch:\n  default: Not_An_Id\n")]
    [InlineData("launch:\n  repos:\n    web: { default: X }\n")]
    [InlineData("agents:\n  Claude-Code: { dailyBudget: 1 }\n")]
    public async Task Agent_keys_check_the_pattern_never_the_catalog_bad_shapes(string block)
    {
        _files.Write(Base + block);

        var result = await _files.Source.Load(Token);

        Assert.False(result.IsSuccess);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Agent_keys_check_the_pattern_never_the_catalog()
    {
        _files.Write(Base + "launch:\n  default: no-such-agent-on-this-box\nagents:\n  another-unknown: { dailyBudget: 3 }\n");

        var result = await _files.Source.Load(Token);

        Assert.True(result.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SoftLimit_cannot_collide_with_an_agent_id()
    {
        Assert.False(AgentId.TryParse(AgentSettings.SoftLimitKey, provider: null, out _));
        _files.Write(Base + "agents:\n  softLimit: { dailyBudget: 1 }\n");

        Assert.False((await _files.Source.Load(Token)).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"0.8\"")]
    [InlineData("[1]")]
    [InlineData("{ a: 1 }")]
    [InlineData("true")]
    public async Task A_mistyped_softLimit_is_a_profile_error_not_a_crash(string value)
    {
        _files.Write(Base + "agents:\n  softLimit: " + value + "\n");

        var loaded = await _files.Source.Load(Token);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, loaded.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("0")]
    [InlineData("1.5")]
    public void SoftLimit_is_a_fraction_above_zero_and_at_most_one(string value)
    {
        var profile = new SindriProfile(1, "thor", Agents: new AgentSettings(SoftLimit: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Contains("agents.softLimit", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_budget_a_window_and_a_hash_that_cant_be_one_name_their_key_path()
    {
        var badBudget = new SindriProfile(1, "thor", Agents: new AgentSettings(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { ["codex"] = new(DailyBudget: -1m) }));
        var badWindow = new SindriProfile(1, "thor", Launch: new LaunchSettings(ConfirmWindow: "soon"));
        var emptyHash = new SindriProfile(1, "thor", Agents: new AgentSettings(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { ["codex"] = new(StepArgvHash: [""]) }));
        var badKey = new SindriProfile(1, "thor", Agents: new AgentSettings(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { ["Bad"] = new() }));

        Assert.Contains("agents.codex.dailyBudget", ProfileRules.Check(badBudget).Error.Message, StringComparison.Ordinal);
        Assert.Contains("launch.confirmWindow", ProfileRules.Check(badWindow).Error.Message, StringComparison.Ordinal);
        Assert.Contains("agents.codex.stepArgvHash[0]", ProfileRules.Check(emptyHash).Error.Message, StringComparison.Ordinal);
        Assert.Contains("agents.Bad", ProfileRules.Check(badKey).Error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Launch: new LaunchSettings(ConfirmWindow: "2h"))).IsSuccess);
    }
}
