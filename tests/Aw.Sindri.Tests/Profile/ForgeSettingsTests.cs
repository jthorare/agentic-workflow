using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>
/// The profile's 'workflow.forge' keys (PR babysitter spec W27): adapter, hosts and credential. A profile without them
/// keeps its canonical JSON, so an earlier approval still holds; the credential value itself is never a token.
/// </summary>
public sealed class ForgeSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(ForgeSettings forge) => _valid with { Workflow = new(Forge: forge) };

    [Theory, Trait("Lane", "unit")]
    [InlineData("github", "github.com", "env:AW_FORGE_TOKEN")]
    [InlineData("file", "forge.test", "command:gh auth token")]
    [InlineData(null, null, null)]
    public void A_valid_forge_block_is_accepted(string? adapter, string? host, string? credential) =>
        Assert.True(ProfileRules.Check(With(new ForgeSettings(adapter, host is null ? null : [host], credential))).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("gitlab", null, null, "workflow.forge.adapter")]
    [InlineData(null, "", null, "workflow.forge.hosts[0]")]
    [InlineData(null, "https://github.com", null, "workflow.forge.hosts[0]")]
    [InlineData(null, null, "ghp_literal_token_value", "workflow.forge.credential")]
    [InlineData(null, null, "env:lower", "workflow.forge.credential")]
    [InlineData(null, null, "command:", "workflow.forge.credential")]
    public void A_bad_forge_key_is_refused_naming_the_key_and_never_echoing_the_value(string? adapter, string? host, string? credential, string key)
    {
        var error = ProfileRules.Check(With(new ForgeSettings(adapter, host is null ? null : [host], credential))).Error;

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_literal_token_value", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_each_forge_key_that_changes()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(
            ["workflow.forge.adapter", "workflow.forge.credential", "workflow.forge.hosts"],
            ProfileDiff.Between(live, With(new ForgeSettings("github", ["github.com"], "env:AW_FORGE_TOKEN"))));
        Assert.Equal(
            ["workflow.forge.hosts"],
            ProfileDiff.Between(With(new ForgeSettings("github", ["github.com"])), With(new ForgeSettings("github", ["github.com", "git.corp.test"]))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_block_keeps_its_canonical_json_and_hash()
    {
        var before = ProfileRules.Check(_valid with { Workflow = new("1m") }).Value;
        var json = AwJson.Serialize(before, SliceHarness.Json);

        Assert.DoesNotContain("forge", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.NotEqual(
            ProfileHashing.Of(json),
            ProfileHashing.Of(AwJson.Serialize(before with { Workflow = new("1m", Forge: new ForgeSettings("file")) }, SliceHarness.Json)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_block_and_refuses_an_unknown_forge_key()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  forge:\n    adapter: github\n    hosts: [github.com]\n    credential: env:AW_FORGE_TOKEN\n");
        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal("github", loaded.Value.Profile.Workflow?.Forge?.Adapter);
        Assert.Equal(["github.com"], loaded.Value.Profile.Workflow?.Forge?.Hosts);

        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  forge:\n    token: secret\n");
        var refused = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
    }
}
