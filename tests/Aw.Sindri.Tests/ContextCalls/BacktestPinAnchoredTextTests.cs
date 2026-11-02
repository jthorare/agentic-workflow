using Aw.Context.Core.Eval.Backtest;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The pin-anchored line under each ranker arm, and the note that the hand check decides when there are fewer than 20 such questions.</summary>
public sealed class BacktestPinAnchoredTextTests
{
    private static BacktestArmScore Score() => new(Samples: 30, NodeRecall: 0.5, NodePrecision: 0.5, FileRecallAt20: 0.5, LeakyShare: 1.0, Tokens: 0);

    private static RunContextBacktestResult Result(int pinned) => new(30, SmallSample: false, LeftOut: 0,
        [new BacktestArmLine("ranker-features", NotAvailable: null, Overall: null, Leaky: false, Score()) { PinAnchored = new PinAnchoredScore(pinned, RecallAt10: 0.5, Mrr: 0.25) },
         new BacktestArmLine("ranker-pin", NotAvailable: null, Overall: null, Leaky: false, Score()) { PinAnchored = new PinAnchoredScore(pinned, RecallAt10: 0.61, Mrr: 0.44) },
         new BacktestArmLine("all", NotAvailable: null, Overall: null, Leaky: false, Score())],
        PassLine: null, "/home/x/backtest.json", []);

    [Fact, Trait("Lane", "unit")]
    public void Under_twenty_pin_anchored_questions_print_the_hand_check_note_once()
    {
        var text = BacktestJobText.Render(Result(pinned: 12));

        Assert.Contains("    pin-anchored (12 questions): recall@10 0.61, MRR 0.44\n", text, StringComparison.Ordinal);
        _ = Assert.Single(text.Split('\n'), l => l == "fewer than 20 pin-anchored questions: the hand check in tests/fixtures/backtest/pin-questions.md decides");
    }

    [Fact, Trait("Lane", "unit")]
    public void Twenty_pin_anchored_questions_print_no_note_and_a_source_arm_prints_no_pin_line()
    {
        var text = BacktestJobText.Render(Result(pinned: 20));

        Assert.DoesNotContain("hand check", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split('\n').Count(l => l.Contains("pin-anchored (20 questions)", StringComparison.Ordinal)));
    }
}
