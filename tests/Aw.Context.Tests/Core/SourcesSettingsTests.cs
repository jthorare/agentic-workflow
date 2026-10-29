using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

/// <summary>'context.sources' (spec §8.8): the retired onyx entry and the Linear teams its identifier aliases may name.</summary>
public sealed class SourcesSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void No_sources_means_no_teams()
    {
        var settings = Read("{}").Value;

        Assert.Empty(settings.LinearTeams);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"url":"http://127.0.0.1:3189","token":"env:ONYX_KEY","version":"v4.9.0"}""")]
    [InlineData("""{"url":"http://127.0.0.1:3189","token":"keychain:onyx/sindri-onyx","version":"v4.9.0","managed":true}""")]
    [InlineData("""{"url":"http://10.0.0.5:3189","token":"a-key-written-in-the-profile","version":"latest","apiKey":"x"}""")]
    [InlineData("\"http://127.0.0.1:3189\"")]
    public void A_profile_that_still_carries_the_retired_onyx_entry_loads_and_ignores_it(string onyx)
    {
        var read = Read("""{"sources":{"onyx":""" + onyx + ""","linear":{"teams":["SPL","APP"]}}}""");

        Assert.False(read.IsError);
        Assert.Equal(["SPL", "APP"], read.Value.LinearTeams, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_linear_teams_are_read_through_their_rules()
    {
        var settings = Read("""{"sources":{"linear":{"teams":["SPL","APP"]}}}""").Value;

        Assert.Equal(["SPL", "APP"], settings.LinearTeams, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_linear_entry_with_a_token_gives_the_native_adapters_settings_and_teams_alone_give_none()
    {
        var native = Read("""{"sources":{"linear":{"token":"keychain:linear/work-read","teams":["SPL"]}}}""").Value;
        var teamsOnly = Read("""{"sources":{"linear":{"teams":["SPL"]}}}""").Value;

        Assert.Equal(("keychain:linear/work-read", "api.linear.app"), (native.Linear!.Token, native.Linear.Address));
        Assert.Equal(["SPL"], native.Linear.Teams, StringComparer.Ordinal);
        Assert.Equal(["SPL"], native.LinearTeams, StringComparer.Ordinal);
        Assert.Null(teamsOnly.Linear);
        Assert.Equal(["SPL"], teamsOnly.LinearTeams, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"apiKey":"x"}}}""", "SND-CONTEXT-001", "context.sources.linear.apiKey:")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"url":"https://evil.example/graphql","token":"env:K"}}}""", "SND-CONTEXT-070", "context.sources.linear.url:")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"url":"https://api.linear.app/graphql"}}}""", "SND-CONTEXT-002", "context.sources.linear.token:")]
    [InlineData("""{"sources":[]}""", "SND-CONTEXT-001", "context.sources:")]
    [InlineData("""{"sources":{"linear":{"teams":["eng team"]}}}""", "SND-CONTEXT-001", "context.sources.linear.teams:")]
    [InlineData("""{"sources":{"linear":{"teams":["E"]}}}""", "SND-CONTEXT-001", "context.sources.linear.teams:")]   // one letter can't prefix an alias
    public void A_bad_sources_entry_is_refused_with_its_code_and_key_path(string json, string code, string keyPath)
    {
        var read = Read(json);

        Assert.Equal(code, read.Error.Code);
        Assert.StartsWith(keyPath, read.Error.Message, StringComparison.Ordinal);
    }
}
