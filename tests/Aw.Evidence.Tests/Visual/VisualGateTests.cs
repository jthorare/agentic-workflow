using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Visual;

public sealed class VisualGateTests
{
    private const string After = "/runs/run-1/desktop-2-click.png";
    private const string Baseline = "/baselines/main.png";
    private static readonly FakeImageCodec _codec = new();
    private static readonly VerdictCacheContext _context = new("visual-critique/v1", "judge-default", AppBuild: "abc", "s", Fixtures: null, "desktop", "140.0", JevImages: false);

    private static byte[] Png(RgbaImage image) => _codec.Encode(image);

    private static RgbaImage Plain() => RgbaImage.Filled(60, 60, 0xFFFFFFFF);

    private static RgbaImage WithDot() => TestImages.Draw(60, 60, static (x, y) => (x, y) == (30, 30) ? 0x000000FFu : 0xFFFFFFFFu);

    private static VisualGateInput Input(bool failedSteps = false, string? baseline = Baseline, VerdictCache? cache = null, VerdictCacheContext? context = null) =>
        new(After, baseline, failedSteps, RunInstanceId.From("instance-1"), "/runs/run-1", context ?? _context, cache ?? VerdictCache.Empty);

    private static async Task<(VisualOutcome Outcome, InvocationLog Log)> Decide(InMemoryRunFiles files, FakeJudge judge, VisualGateInput input)
    {
        var log = new InvocationLog();
        var outcome = await new VisualGate(files, _codec, judge, new FakeTimeProvider(), TestJson.Options).DecideAsync(input, log, CancellationToken.None);
        return (outcome, log);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pixels_identical_to_the_baseline_are_unchanged_without_asking_a_model()
    {
        var files = new InMemoryRunFiles().With(After, Png(Plain())).With(Baseline, Png(Plain()));
        var judge = new FakeJudge();

        var (outcome, log) = await Decide(files, judge, Input());

        Assert.Equal(Verdict.Unchanged, outcome.Verdict);
        Assert.Equal(0, outcome.DiffScore);
        Assert.Equal("/runs/run-1/visual-diff.png", outcome.DiffImage);
        Assert.Empty(judge.CritiqueRequests);
        Assert.Empty(log.All);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_with_failed_steps_is_always_critiqued()
    {
        var files = new InMemoryRunFiles().With(After, Png(Plain())).With(Baseline, Png(Plain()));
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksOff, ["error banner"], Usage: null));

        var (outcome, _) = await Decide(files, judge, Input(failedSteps: true));

        Assert.Equal(Verdict.LooksOff, outcome.Verdict);
        Assert.Equal(["error banner"], outcome.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_region_is_cropped_with_padding_and_sent_to_the_critique()
    {
        var files = new InMemoryRunFiles().With(After, Png(WithDot())).With(Baseline, Png(Plain()));
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksRight, [], new InvocationUsage("haiku", 900, OutputTokens: null)));

        var (outcome, log) = await Decide(files, judge, Input());

        var request = Assert.Single(judge.CritiqueRequests);
        Assert.Equal(("/runs/run-1/crop-after.png", "/runs/run-1/crop-baseline.png"), (request.AfterScreenshot, request.BaselineScreenshot));
        Assert.Equal(49, _codec.Decode(files.Files["/runs/run-1/crop-after.png"]).Value.Width);
        Assert.Equal(Verdict.LooksRight, outcome.Verdict);
        Assert.Equal(1.0 / 3600, outcome.DiffScore);
        var invocation = Assert.Single(log.All);
        Assert.Equal((InvocationPhase.VisualCritique, 900L, default(long?), false), (invocation.Phase, invocation.InputTokens, invocation.OutputTokens, invocation.CacheHit));
        _ = Assert.Single(outcome.Cache.Entries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cached_verdict_answers_without_a_model_and_is_recorded_as_a_cache_hit()
    {
        var files = new InMemoryRunFiles().With(After, Png(WithDot())).With(Baseline, Png(Plain()));
        var (firstOutcome, _) = await Decide(files, new FakeJudge().Critiques(new VisualVerdict(VisualDecision.Sloppy, ["gap"], Usage: null)), Input());
        var judge = new FakeJudge();

        var (outcome, log) = await Decide(files, judge, Input(cache: firstOutcome.Cache));

        Assert.Equal(Verdict.Sloppy, outcome.Verdict);
        Assert.Empty(judge.CritiqueRequests);
        Assert.True(Assert.Single(log.All).CacheHit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_critique_fails_closed_to_unchecked_and_caches_nothing()
    {
        var files = new InMemoryRunFiles().With(After, Png(WithDot())).With(Baseline, Png(Plain()));

        var (outcome, log) = await Decide(files, new FakeJudge(), Input());

        Assert.Equal(Verdict.Unchecked, outcome.Verdict);
        Assert.Empty(outcome.Reasons);
        Assert.Empty(outcome.Cache.Entries);
        Assert.False(Assert.Single(log.All).Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_judge_wired_stays_unchecked_without_calling_or_recording_a_model()
    {
        var files = new InMemoryRunFiles().With(After, Png(WithDot())).With(Baseline, Png(Plain()));
        var judge = FakeJudge.Unavailable;

        var (outcome, log) = await Decide(files, judge, Input());

        Assert.Equal(Verdict.Unchecked, outcome.Verdict);
        Assert.Empty(outcome.Cache.Entries);
        Assert.Empty(judge.CritiqueRequests);
        Assert.Empty(log.All);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_size_change_counts_as_fully_changed_and_sends_the_full_images()
    {
        var files = new InMemoryRunFiles().With(After, Png(RgbaImage.Filled(30, 30, 0xFFFFFFFF))).With(Baseline, Png(Plain()));
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksRight, [], Usage: null));

        var (outcome, _) = await Decide(files, judge, Input());

        Assert.Equal(1, outcome.DiffScore);
        Assert.Null(outcome.DiffImage);
        Assert.Equal((After, Baseline), (judge.CritiqueRequests[0].AfterScreenshot, judge.CritiqueRequests[0].BaselineScreenshot));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_baseline_is_never_assumed_unchanged()
    {
        var files = new InMemoryRunFiles().With(After, Png(Plain())).With(Baseline, "not an image"u8.ToArray());
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksRight, [], Usage: null));

        var (outcome, _) = await Decide(files, judge, Input());

        Assert.Equal(Verdict.LooksRight, outcome.Verdict);
        Assert.Null(outcome.DiffScore);
        _ = Assert.Single(judge.CritiqueRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_baseline_the_full_screenshot_is_critiqued()
    {
        var files = new InMemoryRunFiles().With(After, Png(Plain()));
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksRight, [], Usage: null));

        var (outcome, _) = await Decide(files, judge, Input(baseline: null));

        Assert.Equal(Verdict.LooksRight, outcome.Verdict);
        Assert.Equal((After, default(string)), (judge.CritiqueRequests[0].AfterScreenshot, judge.CritiqueRequests[0].BaselineScreenshot));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_screenshot_skips_the_cache_but_still_asks_the_judge()
    {
        var cache = VerdictCache.Empty;
        var judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksOff, [], Usage: null));

        var (outcome, _) = await Decide(new InMemoryRunFiles(), judge, Input(baseline: null, cache: cache));

        Assert.Equal(Verdict.LooksOff, outcome.Verdict);
        Assert.Empty(outcome.Cache.Entries);
    }
}
