using Aw.Sindri.Core.Graph.Blocks;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IGraphTranscriptPort"/> honours (issue 3787): human turns only, newest first, no subagent session, the filters and the time
/// applied; a read in the order asked, with the assistant text just before a turn when asked, and an unknown ref left out.
/// </summary>
public abstract class GraphTranscriptPortContract
{
    private static readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port under test, over these sessions.</summary>
    /// <param name="sessions">The sessions to seed.</param>
    /// <returns>The port.</returns>
    protected abstract ValueTask<IGraphTranscriptPort> Create(IReadOnlyList<TranscriptSeed> sessions);

    [Fact]
    public async Task Human_turns_are_listed_newest_first_and_assistant_turns_subagents_and_older_turns_are_not()
    {
        var port = await Create([
            Session("transcript:0000000a", "claude-code", "/r/a", subagent: false, Turn(1, human: true, hoursAgo: 30, "first ask"), Turn(2, human: false, hoursAgo: 29, "an answer"), Turn(5, human: true, hoursAgo: 2, "second ask")),
            Session("transcript:0000000b", "codex", "/r/b", subagent: false, Turn(3, human: true, hoursAgo: 10, "codex ask"), Turn(9, human: true, hoursAgo: 400, "too old")),
            Session("transcript:0000000c", "claude-code", "/r/a", subagent: true, Turn(1, human: true, hoursAgo: 1, "a parent's prompt")),
        ]);

        var listed = await port.HumanTurns(new TranscriptTurnQuery(_now.AddDays(-7), [], Repo: null), Token);

        Assert.Equal(["transcript:0000000a#5", "transcript:0000000b#3", "transcript:0000000a#1"], listed.Value.Select(static t => t.Ref), StringComparer.Ordinal);
        Assert.Equal("transcript:0000000b", listed.Value[1].Session);
    }

    [Fact]
    public async Task The_provider_and_repo_filters_narrow_the_list()
    {
        var port = await Create([
            Session("transcript:0000000a", "claude-code", "/r/a", subagent: false, Turn(1, human: true, hoursAgo: 3, "a")),
            Session("transcript:0000000b", "codex", "/r/a", subagent: false, Turn(1, human: true, hoursAgo: 2, "b")),
            Session("transcript:0000000d", "claude-code", "/r/other", subagent: false, Turn(1, human: true, hoursAgo: 1, "d")),
        ]);

        var codex = await port.HumanTurns(new TranscriptTurnQuery(_now.AddDays(-7), ["codex"], Repo: null), Token);
        var repo = await port.HumanTurns(new TranscriptTurnQuery(_now.AddDays(-7), [], "/r/a"), Token);

        Assert.Equal(["transcript:0000000b#1"], codex.Value.Select(static t => t.Ref), StringComparer.Ordinal);
        Assert.Equal(["transcript:0000000b#1", "transcript:0000000a#1"], repo.Value.Select(static t => t.Ref), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_read_keeps_the_order_asked_adds_the_assistant_text_before_and_leaves_out_unknown_refs()
    {
        var port = await Create([
            Session("transcript:0000000a", "claude-code", "/r/a", subagent: false, Turn(1, human: true, hoursAgo: 5, "first ask"), Turn(2, human: false, hoursAgo: 4, "the answer"), Turn(7, human: true, hoursAgo: 3, "no, the other way")),
        ]);

        var read = await port.Read(["transcript:0000000a#7", "transcript:ffffffff#1", "transcript:0000000a#1"], withBefore: true, Token);
        var bare = await port.Read(["transcript:0000000a#7"], withBefore: false, Token);

        Assert.Equal([("transcript:0000000a#7", "no, the other way", "the answer"), ("transcript:0000000a#1", "first ask", (string?)null)], read.Value.Select(static t => (t.Ref, t.Text, t.Before)));
        Assert.Null(Assert.Single(bare.Value).Before);
    }

    private static TranscriptSeed Session(string session, string provider, string? repo, bool subagent, params TranscriptSeedTurn[] turns) => new(session, provider, repo, subagent, turns);

    private static TranscriptSeedTurn Turn(int line, bool human, int hoursAgo, string text) => new(line, human, _now.AddHours(-hoursAgo), text);
}
