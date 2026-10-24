using Aw.Sindri.Core.Line;
using Aw.Sindri.Host.Commands.Line;

namespace Aw.Sindri.Tests.Commands;

/// <summary>A session caller reads the line board's park reasons and next sentences inside the pack fence (LN-API1); ids, states, links and numbers stay as they are. 'line show' prints the board in plan order.</summary>
public sealed class LineViewFencingTests
{
    private static readonly LineView _view = new(
        [new LineStation("scoping", Instance: null, new LinePacket("scoping", "sha256:" + new string('1', 64), Frozen: true)), new LineStation("design", Instance: null, Packet: null)],
        [
            new LinePhaseView(LinePhaseId.From("p1"), Attempt: 1, "T-3/line/1/phase.p1.1", "failed", "out of budget", "https://example.test/pull/1", "default-branch", "/work/p1", "Parked.", Tokens: 10, WallMinutes: 5),
            new LinePhaseView(LinePhaseId.From("p2"), Attempt: 0, Address: null, "waiting", ParkReason: null, Pr: null, Base: null, Worktree: null, string.Empty, Tokens: 0, WallMinutes: 0),
        ],
        Parallel: 2,
        MaxOpenPrs: 4);

    [Fact, Trait("Lane", "unit")]
    public void The_texts_are_inside_the_fence_and_the_structure_is_untouched()
    {
        var fenced = LineViewFencing.Fence(_view);

        Assert.NotEqual("out of budget", fenced.Phases[0].ParkReason, StringComparer.Ordinal);
        Assert.Contains("out of budget", fenced.Phases[0].ParkReason, StringComparison.Ordinal);
        Assert.NotEqual("Parked.", fenced.Phases[0].Now, StringComparer.Ordinal);
        Assert.Equal(("p1", "failed", "https://example.test/pull/1", "/work/p1"), (fenced.Phases[0].Id.Value, fenced.Phases[0].State, fenced.Phases[0].Pr, fenced.Phases[0].Worktree));
        Assert.Equal((null, string.Empty), (fenced.Phases[1].ParkReason, fenced.Phases[1].Now));
        Assert.Equal(_view.Stations, fenced.Stations);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_board_text_lists_the_chain_then_the_phases_in_plan_order()
    {
        var text = LineBoardText.Board(_view);

        Assert.Contains("scoping: sha256:" + new string('1', 64) + " (frozen)", text, StringComparison.Ordinal);
        Assert.Contains("design: no packet yet", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("p1 (attempt 1): failed", StringComparison.Ordinal) < text.IndexOf("p2 (attempt 0): waiting", StringComparison.Ordinal));
        Assert.Contains("at most 2 running, 4 open PRs", text, StringComparison.Ordinal);
    }
}
