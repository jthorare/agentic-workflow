using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Callers;

public sealed class CallerHeaderParsingTests
{
    [Fact, Trait("Lane", "unit")]
    public void Missing_caller_header_is_session()
    {
        var parsed = CallerHeaderParsing.Parse(caller: null, terminal: null, client: null, repo: null);

        Assert.Equal(CallerClass.Session, parsed.Value.Class);
        Assert.Equal(ClientLabel.None, parsed.Value.Client);
        Assert.Null(parsed.Value.Repo);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("human", CallerClass.Human)]
    [InlineData("session", CallerClass.Session)]
    public void Human_and_session_values_map_to_their_classes(string value, CallerClass expected) =>
        Assert.Equal(expected, CallerHeaderParsing.Parse(value, terminal: null, client: null, repo: null).Value.Class);

    [Theory, Trait("Lane", "unit")]
    [InlineData("admin")]
    [InlineData("Human")]
    [InlineData("service")]
    [InlineData("")]
    [InlineData("human,session")]
    public void Unknown_caller_value_is_SND_API_007(string value)
    {
        var parsed = CallerHeaderParsing.Parse(value, terminal: null, client: null, repo: null);

        Assert.Equal("SND-API-007", parsed.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("human", "1", true)]
    [InlineData("human", null, false)]
    [InlineData("human", "yes", false)]
    [InlineData("session", "1", false)]
    [InlineData(null, "1", false)]
    public void Terminal_is_honoured_only_with_human(string? caller, string? terminal, bool expected) =>
        Assert.Equal(expected, CallerHeaderParsing.Parse(caller, terminal, client: null, repo: null).Value.Terminal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("cli", ClientLabel.Cli)]
    [InlineData("ui", ClientLabel.Ui)]
    [InlineData("prover", ClientLabel.Prover)]
    [InlineData("mcp", ClientLabel.Mcp)]
    public void Client_label_sets_the_actor(string client, ClientLabel expected)
    {
        var parsed = CallerHeaderParsing.Parse("human", terminal: null, client, repo: null).Value;

        Assert.Equal(expected, parsed.Client);
        Assert.Null(parsed.UnknownClient);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("browser")]
    [InlineData("CLI")]
    [InlineData("none")]
    public void Unknown_client_label_counts_as_none(string client)
    {
        var parsed = CallerHeaderParsing.Parse("human", terminal: null, client, repo: null).Value;

        Assert.Equal(ClientLabel.None, parsed.Client);
        Assert.Equal(client, parsed.UnknownClient);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_header_scopes_the_caller() =>
        Assert.Equal("agentic-workflow", CallerHeaderParsing.Parse(caller: null, terminal: null, client: null, "agentic-workflow").Value.Repo?.Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("../etc")]
    [InlineData("a b")]
    [InlineData("")]
    public void A_malformed_repo_header_is_SND_API_007(string repo) =>
        Assert.Equal("SND-API-007", CallerHeaderParsing.Parse(caller: null, terminal: null, client: null, repo).Error.Code);

    private const string Launch = "LCH-00000000000000000000000000000001";

    [Fact, Trait("Lane", "unit")]
    public void A_launch_header_is_kept_for_a_session() =>
        Assert.Equal(LaunchId.From(Launch), CallerHeaderParsing.Parse("session", terminal: null, client: null, repo: null, Launch).Value.Launch);

    [Fact, Trait("Lane", "unit")]
    public void No_launch_header_means_no_launch() =>
        Assert.Null(CallerHeaderParsing.Parse(caller: null, terminal: null, client: null, repo: null).Value.Launch);

    [Fact, Trait("Lane", "unit")]
    public void A_launch_makes_a_human_header_a_launched_session()
    {
        var parsed = CallerHeaderParsing.Parse("human", terminal: "1", client: null, repo: null, Launch).Value;

        Assert.Equal((CallerClass.Session, false, LaunchId.From(Launch)), (parsed.Class, parsed.Terminal, parsed.Launch));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not-a-launch")]
    [InlineData("")]
    [InlineData("LCH-ABC")]
    public void A_malformed_launch_header_is_SND_API_007(string launch) =>
        Assert.Equal("SND-API-007", CallerHeaderParsing.Parse(caller: null, terminal: null, client: null, repo: null, launch).Error.Code);
}
