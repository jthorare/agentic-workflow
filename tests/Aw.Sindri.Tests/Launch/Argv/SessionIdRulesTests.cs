using System.Diagnostics;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Argv;

/// <summary>Session ids are bounded generically, then checked against the entry (launcher L-9).</summary>
public sealed class SessionIdRulesTests
{
    private const string Uuid = "0b1f6c3e-52d4-4c7a-9a58-3d1e7f9a2b10";

    private static SessionId Id(string text) => SessionId.From(text);

    private static AgentEntry WithPattern(string pattern) =>
        new(AgentEntries.Aider.Id, "Aider", "aider", ProcessName: null, Transcripts: null, Resume: null) { Open = ["{bin}"], SessionIdPattern = pattern };

    [Theory, Trait("Lane", "unit")]
    [InlineData("..")]
    [InlineData("a/../b")]
    [InlineData("a..b")]
    [InlineData(".hidden")]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public void Session_id_bound_refuses_dotdot_and_a_leading_dot(string id) => Assert.Null(SessionIdRules.Parse(id));

    [Fact, Trait("Lane", "unit")]
    public void Session_id_bound_refuses_129_characters_and_accepts_128()
    {
        Assert.True(SessionIdRules.Parse(new string('a', 128)) is not null);
        Assert.Null(SessionIdRules.Parse(new string('a', 129)));
        Assert.True(SessionIdRules.Parse("a.b:c_d-1") is not null);
        Assert.True(SessionIdRules.IsUsable(AgentEntries.Aider, SessionIdRules.Parse("a.b:c_d-1")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_null_id_is_not_usable() => Assert.False(SessionIdRules.IsUsable(AgentEntries.ClaudeCode, id: null));

    [Fact, Trait("Lane", "unit")]
    public void The_entry_pattern_must_match_too()
    {
        Assert.True(SessionIdRules.IsUsable(AgentEntries.ClaudeCode, Id(Uuid)));
        Assert.False(SessionIdRules.IsUsable(AgentEntries.ClaudeCode, Id("not-a-uuid")));
    }

    [Fact, Trait("Lane", "unit")]
    public void User_patterns_run_NonBacktracking_with_a_100ms_timeout()
    {
        // (a+)+$ takes exponential time on a backtracking engine for 'aaaa…:'; NonBacktracking is linear, so this returns at once.
        var entry = WithPattern("^(a+)+$");
        var id = new string('a', 100) + ":";

        var clock = Stopwatch.StartNew();
        var usable = SessionIdRules.IsUsable(entry, Id(id));

        Assert.False(usable);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pattern_that_is_not_a_valid_regex_makes_no_id_usable()
    {
        var entry = WithPattern("(unclosed");

        Assert.False(SessionIdRules.IsUsable(entry, Id(Uuid)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_backreference_pattern_that_NonBacktracking_cannot_run_makes_no_id_usable()
    {
        var entry = WithPattern(@"^(a)\1$");

        Assert.False(SessionIdRules.IsUsable(entry, Id("aa")));
    }
}
