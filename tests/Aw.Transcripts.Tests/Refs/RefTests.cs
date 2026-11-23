using System.Text;
using Aw.Kernel.Json;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Tests.Refs;

public sealed class RefTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_session_ref_is_eight_hex_digits_and_the_same_on_every_call()
    {
        var first = TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, "/home/dev/.claude/projects/-work-alpha/main.jsonl");
        var again = TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, "/home/dev/.claude/projects/-work-alpha/main.jsonl");

        Assert.Matches("^transcript:[0-9a-f]{8}$", first.Value);
        Assert.Equal(first, again);
        Assert.DoesNotContain("alpha", first.Value, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_provider_is_part_of_a_session_ref()
    {
        var claude = TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, "/x/a.jsonl");
        var cursor = TranscriptRefs.ForFile(TranscriptProvider.Cursor, "/x/a.jsonl");

        Assert.NotEqual(claude, cursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_turn_ref_names_its_session_and_line()
    {
        var session = SessionRef.From("transcript:0a1b2c3d");

        var turn = TranscriptRefs.ForTurn(session, 42);

        Assert.Equal("transcript:0a1b2c3d#42", turn.Value);
        Assert.Equal(session, turn.SessionOf());
        Assert.Equal(42, turn.Line);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("transcript:0A1B2C3D")]
    [InlineData("transcript:0a1b2c3")]
    [InlineData("transcript:0a1b2c3d#1")]
    [InlineData("/home/dev/main.jsonl")]
    public void A_session_ref_refuses_any_other_shape(string text) => Assert.False(SessionRef.TryFrom(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData("transcript:0a1b2c3d")]
    [InlineData("transcript:0a1b2c3d#0")]
    [InlineData("transcript:0a1b2c3d#-1")]
    [InlineData("transcript:0a1b2c3d#01")]
    public void A_turn_ref_refuses_any_other_shape(string text) => Assert.False(TurnRef.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void Refs_round_trip_through_json_as_strings()
    {
        var options = AwJson.CreateOptions(RefsJsonContext.Default);
        var session = SessionRef.From("transcript:0a1b2c3d");
        var turn = TurnRef.From("transcript:0a1b2c3d#7");

        var sessionJson = Encoding.UTF8.GetString(AwJson.Serialize(session, options));
        var turnJson = Encoding.UTF8.GetString(AwJson.Serialize(turn, options));

        Assert.Equal("\"transcript:0a1b2c3d\"", sessionJson);
        Assert.Equal("\"transcript:0a1b2c3d#7\"", turnJson);
        Assert.Equal(session, AwJson.Deserialize<SessionRef>(Encoding.UTF8.GetBytes(sessionJson), options).Value);
        Assert.Equal(turn, AwJson.Deserialize<TurnRef>(Encoding.UTF8.GetBytes(turnJson), options).Value);
        Assert.True(AwJson.Deserialize<SessionRef>("\"nope\""u8, options).IsError);
    }
}
