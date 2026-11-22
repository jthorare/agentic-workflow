using Aw.Sindri.Core.Home;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IMetaTeamReader"/> honours (home spec §3.3): a team reads back field by field (lanes with their tmux session names, the merge queue in order,
/// usage with a limited subject's kind and reset time, the repository directory), and a box with no team reads as unavailable with nothing in it.
/// </summary>
public abstract class MetaTeamReaderContract
{
    /// <summary>The worst case a box shows: a lane at the mark with a session, one with unknown size, two queued PRs (one in GitHub's queue), a limited subject with a reset time and one without.</summary>
    protected static MetaTeamRead Team { get; } = new(
        200_000,
        [
            new MetaLane("meta", "sindri-v3-meta", "6f1c2a9e-0000-4000-8000-000000000001", 205_431, 200_000, "Recycle"),
            new MetaLane("ideas", "sindri-v3-ideas", Session: null, Tokens: null, 200_000, "Unknown"),
        ],
        [new MetaQueuedPr(3099, "sindri-v3-meta", "Pass", Github: null), new MetaQueuedPr(3101, "sindri-v3-ui", "none", "queued 2 (AWAITING_CHECKS)")],
        [
            new MetaUsage("ui", "Limited", "Weekly", new DateTimeOffset(2026, 11, 14, 17, 0, 0, TimeSpan.Zero), "claude-code"),
            new MetaUsage("qa", "Limited", "Other", ResetsAt: null, Agent: null),
            new MetaUsage("meta", "Ok", Kind: null, ResetsAt: null, "claude-code"),
        ],
        "/home/owner/repos/agentic-workflow",
        Unavailable: null);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A reader that reads <paramref name="team"/>; for <see cref="MetaTeamRead.NotAvailable"/>, a box with no aw-meta.</summary>
    /// <param name="team">What the box holds.</param>
    /// <returns>The reader.</returns>
    protected abstract IMetaTeamReader Create(MetaTeamRead team);

    /// <summary>Every field reads back as the box holds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_team_reads_back_field_by_field()
    {
        var read = await Create(Team).Read(Token);

        Expect.Equal(expected: null, read.Unavailable);
        Expect.Equal(Team.RecycleAt, read.RecycleAt);
        Expect.True(Team.Lanes.SequenceEqual(read.Lanes));
        Expect.True(Team.MergeQueue.SequenceEqual(read.MergeQueue));
        Expect.True(Team.Usage.SequenceEqual(read.Usage));
        Expect.Equal(Team.RepoDirectory, read.RepoDirectory);
    }

    /// <summary>No team: unavailable, with a reason and nothing else.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_box_with_no_team_reads_as_unavailable_and_empty()
    {
        var read = await Create(MetaTeamRead.NotAvailable("no aw-meta")).Read(Token);

        Expect.True(!string.IsNullOrWhiteSpace(read.Unavailable));
        Expect.Equal(0, read.Lanes.Count);
        Expect.Equal(0, read.MergeQueue.Count);
        Expect.Equal(0, read.Usage.Count);
    }
}
