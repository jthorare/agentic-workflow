using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The Agents page's profile keys (Agents page spec §8, phase A0): <c lang="text">agents.idleOwedMinutes</c>, <c lang="text">agents.slotWaitMinutes</c> and <c lang="text">agents.page</c>.</summary>
public sealed class AgentPageKeysTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_three_keys_beside_the_agent_ids()
    {
        _files.Write(Base + "agents:\n  idleOwedMinutes: 20\n  slotWaitMinutes: 45\n  page: rows\n  claude-code: { dailyBudget: 12.5 }\n");

        var agents = (await _files.Source.Load(Token)).Value.Profile.Agents!;

        Assert.Equal((20, 45, "rows"), (agents.IdleOwedMinutes, agents.SlotWaitMinutes, agents.Page));
        Assert.Equal(12.5m, agents.Targets!["claude-code"].DailyBudget);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_profile_without_the_keys_has_none_and_keeps_its_hash()
    {
        _files.Write(Base + "agents:\n  claude-code: { dailyBudget: 3 }\n");
        var before = (await _files.Source.Load(Token)).Value;

        Assert.Equal((null, null, null), (before.Profile.Agents!.IdleOwedMinutes, before.Profile.Agents.SlotWaitMinutes, before.Profile.Agents.Page));
        var canonical = System.Text.Json.JsonSerializer.Serialize(before.Profile, ProfileFiles.Options);
        Assert.DoesNotContain("idleOwedMinutes", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("slotWaitMinutes", canonical, StringComparison.Ordinal);

        _files.Write(Base + "agents:\n  claude-code: { dailyBudget: 3 }\n  page: legacy\n");
        Assert.NotEqual(before.Hash, (await _files.Source.Load(Token)).Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_defaults_are_ten_and_thirty_minutes()
    {
        Assert.Equal((10, 30, 1, 1440), (AgentSettings.DefaultIdleOwedMinutes, AgentSettings.DefaultSlotWaitMinutes, AgentSettings.MinMinutes, AgentSettings.MaxMinutes));
        var inputs = new Core.Sessions.AgentReasonInputs(DateTimeOffset.UnixEpoch, "qa", DateTimeOffset.UnixEpoch);
        Assert.Equal((AgentSettings.DefaultIdleOwedMinutes, AgentSettings.DefaultSlotWaitMinutes), (inputs.IdleOwedMinutes, inputs.SlotWaitMinutes));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("idleOwedMinutes", 0)]
    [InlineData("idleOwedMinutes", 1441)]
    [InlineData("slotWaitMinutes", -5)]
    [InlineData("slotWaitMinutes", 100000)]
    public void A_minute_count_outside_1_to_1440_is_SND_AGENT_010_with_the_key_path(string key, int minutes)
    {
        var agents = key == "idleOwedMinutes" ? new AgentSettings(IdleOwedMinutes: minutes) : new AgentSettings(SlotWaitMinutes: minutes);

        var result = ProfileRules.Check(new SindriProfile(1, "thor", Agents: agents));

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AgentSettingInvalid.Code, result.Error.Code);
        Assert.Contains("agents." + key, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(1440)]
    public void The_ends_of_the_range_are_valid(int minutes)
    {
        var result = ProfileRules.Check(new SindriProfile(1, "thor", Agents: new AgentSettings(IdleOwedMinutes: minutes, SlotWaitMinutes: minutes, Page: AgentSettings.PageLegacy)));

        Assert.True(result.IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("old")]
    [InlineData("Rows")]
    [InlineData("")]
    public void A_page_that_is_not_rows_or_legacy_is_SND_AGENT_010_with_the_key_path(string page)
    {
        var result = ProfileRules.Check(new SindriProfile(1, "thor", Agents: new AgentSettings(Page: page)));

        Assert.Equal((SindriErrors.AgentSettingInvalid.Code, true), (result.Error.Code, result.Error.Message.Contains("agents.page", StringComparison.Ordinal)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("idleOwedMinutes: \"ten\"")]
    [InlineData("slotWaitMinutes: 1.5")]
    [InlineData("page: 3")]
    [InlineData("idleOwedMinutes: [1]")]
    public async Task A_key_of_the_wrong_type_is_a_profile_error_not_a_crash(string line)
    {
        _files.Write(Base + "agents:\n  " + line + "\n");

        var loaded = await _files.Source.Load(Token);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, loaded.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_keys_show_in_the_approval_diff()
    {
        var approved = new SindriProfile(1, "thor");
        var live = approved with { Agents = new AgentSettings(IdleOwedMinutes: 20, SlotWaitMinutes: 45, Page: AgentSettings.PageRows) };

        var changes = ProfileDiff.Between(approved, live);

        Assert.Contains(changes, static c => c.Contains("agents.idleOwedMinutes", StringComparison.Ordinal));
        Assert.Contains(changes, static c => c.Contains("agents.slotWaitMinutes", StringComparison.Ordinal));
        Assert.Contains(changes, static c => c.Contains("agents.page", StringComparison.Ordinal));
    }
}
