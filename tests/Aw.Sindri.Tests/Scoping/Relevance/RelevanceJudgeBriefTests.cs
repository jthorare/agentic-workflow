using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;
using Microsoft.Extensions.Logging.Abstractions;
using static Aw.Sindri.Tests.Scoping.Relevance.RelevanceFixtures;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>A big brief with many entries (#1440): the brief is scrubbed and capped once for the whole run, and a request is built as its call goes out, not all of them before the first call.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceJudgeBriefTests
{
    private const int EntryCount = 300;

    private static readonly string _longBrief = string.Concat(Enumerable.Repeat("Invitations expire after a chosen number of days. ", 10_000));

    private static ScopingFacts Facts()
    {
        var nodes = Enumerable.Range(1, EntryCount)
            .Select(static i => Node("code:src/f" + i.ToString(CultureInfo.InvariantCulture) + ".ts", "code", "f.ts", "code", "L-code-" + i.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        var lenses = Lenses();
        lenses["code"] = Lens(["surface:screen/invite-dialog"], EntryCount, nodes, [], []);
        var brief = Brief();
        brief["text"] = _longBrief;
        return new ScopingFacts { ["brief"] = brief.ToJsonString(), ["lenses"] = lenses.ToJsonString() };
    }

    private static Task<BlockStepResult> Run(CountingScrubber scrubber, RecordingJudge judge) =>
        new RelevanceJudgeStep(judge, scrubber, TimeProvider.System, NullLogger<RelevanceJudgeStep>.Instance).Run(ScopingStepContexts.Of("step.scope.relevance-judge", Facts()), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task The_brief_is_scrubbed_once_whatever_the_entry_count()
    {
        var scrubber = new CountingScrubber(_longBrief);
        var judge = new RecordingJudge(scrubber);

        _ = await Run(scrubber, judge);

        Assert.Equal(1, scrubber.BriefCalls);
        Assert.Equal(judge.Requests.Count, judge.Requests.Count(static r => r.Facts["entry"].Length > 0));
        Assert.True(judge.Requests.Count >= EntryCount);
    }

    [Fact]
    public async Task Every_request_shares_one_brief_string()
    {
        var scrubber = new CountingScrubber(_longBrief);
        var judge = new RecordingJudge(scrubber);

        _ = await Run(scrubber, judge);

        var requests = judge.Requests;
        Assert.NotEmpty(requests);
        var first = requests[0].Facts["brief"];
        Assert.All(requests, r => Assert.Same(first, r.Facts["brief"]));
    }

    [Fact]
    public async Task The_first_call_goes_out_before_the_other_requests_are_built()
    {
        var scrubber = new CountingScrubber(_longBrief);
        var judge = new RecordingJudge(scrubber);

        _ = await Run(scrubber, judge);

        Assert.InRange(judge.TotalCallsAtFirstChoose, 1, 3);
    }

    private sealed class CountingScrubber(string brief) : ITextScrubber
    {
        private int _total;
        private int _briefCalls;

        public int Total => Volatile.Read(ref _total);

        public int BriefCalls => Volatile.Read(ref _briefCalls);

        public string Scrub(string text)
        {
            _ = Interlocked.Increment(ref _total);
            if (string.Equals(text, brief, StringComparison.Ordinal))
            {
                _ = Interlocked.Increment(ref _briefCalls);
            }

            return text;
        }
    }

    private sealed class RecordingJudge(CountingScrubber scrubber) : IJudgeChoice
    {
        private readonly Lock _gate = new();
        private readonly List<ChooseRequest> _requests = [];

        public int TotalCallsAtFirstChoose { get; private set; } = -1;

        public IReadOnlyList<ChooseRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_requests.Count == 0)
                {
                    TotalCallsAtFirstChoose = scrubber.Total;
                }

                _requests.Add(request);
            }

            return ValueTask.FromResult(new ChooseAnswer(ChooseStatus.Decided, "in", "model", 0.9, "jev", "JD-1"));
        }
    }
}
