using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Features.RunBacktest;
using Aw.Context.Tests.Eval.Backtest;

namespace Aw.Context.Tests.RunBacktest;

public sealed class PinAnchoredReportTests
{
    private static SampleScore Scored(string key, string arm, string? pin, double recall10, double rr) =>
        new(key, arm, Unmapped: false, NodeRecall: 1, NodePrecision: 1, FileRecallAt20: 1, [], [], [], Leaky: false) { Pin = pin, FileRecallAt10 = recall10, ReciprocalRank = rr };

    [Fact, Trait("Lane", "unit")]
    public void Ranker_arms_report_the_pin_anchored_questions_apart()
    {
        var samples = new[] { Builders.Sample("SPL-101"), Builders.Sample("SPL-102"), Builders.Sample("SPL-103") };
        var scores = new Dictionary<Arm, List<SampleScore>>
        {
            [Arm.RankerFeatures] = [Scored("SPL-101", "ranker-features", pin: null, 0.0, 0.0), Scored("SPL-102", "ranker-features", pin: null, 0.5, 0.5), Scored("SPL-103", "ranker-features", pin: null, 1.0, 1.0)],
            [Arm.RankerPin] = [Scored("SPL-101", "ranker-pin", "billing", 1.0, 1.0), Scored("SPL-102", "ranker-pin", "billing", 0.5, 0.25), Scored("SPL-103", "ranker-pin", pin: null, 0.0, 0.0)],
        };
        var request = new BacktestRequest(new ContextScope(Settings: null, [], new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("aw-split-panes"), ContextClient.Http)), [Arm.RankerFeatures, Arm.RankerPin]);

        var report = ReportAssembly.Assemble(request, (DateTimeOffset.UnixEpoch, LeftOut.None), new Collected(samples, scores, ExtractFailed: 0, NoBrief: 0));

        var pin = report.Arms.Single(a => a.Arm == "ranker-pin").PinAnchored;
        Assert.Equal(new PinAnchoredScore(2, 0.75, 0.625), pin);   // SPL-103 is in no pin: the subset is SPL-101 and SPL-102 only
        Assert.Null(report.Arms.Single(a => a.Arm == "ranker-features").PinAnchored);   // no sample of it was pin-anchored
    }
}
