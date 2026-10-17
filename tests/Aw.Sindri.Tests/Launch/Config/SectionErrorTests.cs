using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>Per-section errors (launcher spec section 4): what each costs, and what the launcher is told.</summary>
public sealed class SectionErrorTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_bad_entry_drops_only_that_entry_with_its_line()
    {
        var config = LauncherText.Isolated("{\n  \"schema\": 1,\n  \"agents\": {\n    \"fine\": { \"label\": \"F\", \"bin\": \"f\", \"open\": [\"{bin}\"], \"sessionId\": \"none\" },\n    \"broken\": { \"label\": \"x\" }\n  }\n}\n");

        Assert.Equal(["claude-code", "codex", "cursor", "fine"], config.Agents.Select(static a => a.Id.Value), StringComparer.Ordinal);
        var dropped = Assert.Single(config.Dropped);
        Assert.Equal(("broken", 5), (dropped.Id, dropped.Line));
        Assert.Contains("agents.broken", dropped.Reason, StringComparison.Ordinal);
        Assert.NotNull(config.Problem);
        Assert.False(config.Unusable);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_override_drops_the_override_and_keeps_the_builtin()
    {
        var config = LauncherText.Isolated("{\n \"schema\": 1,\n \"agents\": {\n  \"codex\": { \"override\": true, \"mcp\": \"nope\" }\n }\n}");

        Assert.Equal("Codex", LauncherText.Entry(config, "codex").Label);
        Assert.Equal(("codex", 4), (config.Dropped[0].Id, config.Dropped[0].Line));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_viewer_refuses_attended_opens_only()
    {
        var config = LauncherText.Isolated("""{ "schema": 1, "viewer": { "kind": "nope" } }""");

        _ = Assert.Single(config.AttendedProblems);
        Assert.Null(config.Problem);
        Assert.False(config.Unusable);
        Assert.Equal(3, config.Agents.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("""{ "schema": 2 }""")]
    [InlineData("""[1]""")]
    public void An_unparseable_file_refuses_opens_and_parks_steps(string text)
    {
        var config = LauncherText.Isolated(text);

        Assert.True(config.Unusable);
        Assert.NotNull(config.Problem);
        Assert.Equal(["claude-code", "codex", "cursor"], config.Agents.Select(static a => a.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unparseable_file_says_the_line_of_the_parse_error()
    {
        var config = LauncherText.Isolated("{\n  \"schema\": 1,\n  \"agents\": {,\n}");

        Assert.Equal(3, config.ProblemLine);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sessions_and_meta_sections_are_accepted_unread()
    {
        var config = LauncherText.Isolated("""{ "schema": 1, "sessions": { "pollSeconds": 7 }, "meta": { "lanesAgent": "codex", "whatever": 1 } }""");

        Assert.Equal((7, null, false), (config.Sessions!.PollSeconds, config.Problem, config.Unusable));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{ "schema": 1, "extra": 1 }""")]
    [InlineData("""{ "schema": 1, "viewer": { "kind": "print", "extra": 1 } }""")]
    [InlineData("""{ "schema": 1, "host": { "kind": "tmux", "extra": 1 } }""")]
    [InlineData("""{ "schema": 1, "agents": { "codex": { "override": true, "extra": 1 } } }""")]
    [InlineData("""{ "schema": 1, "sessions": { "extra": 1 } }""")]
    [InlineData("""{ "schema": 1, "agents": { "a": { "label": "A", "bin": "a", "open": ["{bin}"], "sessionId": "pre-chosen-uuid", "sessionIdPattern": "x", "reportedId": { "extra": 1 } } } }""")]
    public void Unknown_key_in_any_section_is_SND_LAUNCH_011(string text)
    {
        var read = Infrastructure.Launch.LauncherConfigFile.Read(SessionFixtures.Builtins(), LauncherText.Bytes(text));

        Assert.Equal("SND-LAUNCH-011", read.Error.Code);
    }
}
