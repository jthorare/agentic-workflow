using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Labeling;

/// <summary>When Jev refuses a pack, and when it can't be asked at all (spec 6.4's table, one test per row).</summary>
[Trait("Lane", "unit")]
public sealed class ModelLabelerSplitTests
{
    private static readonly LabelOptions _on = new(Pick: true, Candidates: 2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Id(int i) => "h" + i.ToString("D5", CultureInfo.InvariantCulture);

    private static List<FileCluster> Clusters(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new FileCluster(ClusterId.From(Id(i)), [string.Create(CultureInfo.InvariantCulture, $"src/f{i}/a.ts")], 1.0) { FileName = string.Create(CultureInfo.InvariantCulture, $"src/f{i}") })];

    // Every cluster gets the candidates 'First <id>' and 'Second <id>', whichever pack asks.
    private static FakeModelPort Model(int count) => new()
    {
        Responder = _ => "[" + string.Join(',', Enumerable.Range(0, count).Select(i => $$"""{"id":"{{Id(i)}}","labels":["First {{Id(i)}}","Second {{Id(i)}}"]}""")) + "]",
    };

    private static Task<Result<LabelPass>> Run(FakeChoicePort port, int count) =>
        new ModelLabeler(Model(count), new RedactionScrubberFactory(new FakeTimeProvider()), port).Label(Clusters(count), [], 10_000_000, _on, Ct).AsTask();

    private static Result<IReadOnlyList<ChoiceAnswer>> Second(IReadOnlyList<ChoiceQuestion> questions) =>
        Results.Success<IReadOnlyList<ChoiceAnswer>>([.. questions.Select(q => new ChoiceAnswer(q.Key, q.Options[1], 0.9))]);

    [Fact]
    public async Task A_rejected_pack_splits_into_two_halves_and_each_half_is_asked_once()
    {
        var port = new FakeChoicePort { Script = qs => qs.Count > 5 ? ContextErrors.ChoiceRejected.With("http-413") : Second(qs) };

        var pass = await Run(port, 10);

        Assert.Equal([10, 5, 5], port.Asked.Select(a => a.Questions.Count));
        Assert.Equal(10, pass.Value.Labels.Count(l => l.Label.StartsWith("Second", StringComparison.Ordinal)));
        Assert.Equal(new LabelHow(Jev: 10, 0, 0), pass.Value.Chosen);
    }

    [Fact]
    public async Task A_half_that_fails_the_same_way_splits_once_more_and_depth_stops_at_two()
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: true, "unparseable-answer") };

        var pass = await Run(port, 8);

        Assert.Equal([8, 4, 2, 2, 4, 2, 2], port.Asked.Select(a => a.Questions.Count));   // 1 + 2 + 4 calls, never down to single questions
        Assert.Equal(new LabelHow(0, FirstCandidate: 8, 0), pass.Value.Chosen);
        Assert.All(pass.Value.Labels, l => Assert.StartsWith("First", l.Label, StringComparison.Ordinal));
        Assert.Null(pass.Value.PickUnavailable);
    }

    [Fact]
    public async Task A_pack_of_a_hundred_makes_at_most_seven_calls_down_to_twenty_five_questions()
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: true, "timeout") };

        _ = await Run(port, 100);

        Assert.Equal([100, 50, 25, 25, 50, 25, 25], port.Asked.Select(a => a.Questions.Count));
    }

    [Fact]
    public async Task One_question_alone_that_is_rejected_takes_its_fallback_and_is_not_asked_again()
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: true, "http-422") };

        var pass = await Run(port, 1);

        _ = Assert.Single(port.Asked);
        Assert.Equal("First " + Id(0), Assert.Single(pass.Value.Labels).Label);
    }

    [Theory]
    [InlineData("no-api-key")]
    [InlineData("network-error")]
    [InlineData("excluded")]
    [InlineData("http-401")]
    [InlineData("http-403")]
    [InlineData("http-429")]
    [InlineData("http-500")]
    public async Task A_provider_that_cant_be_asked_stops_the_pick_for_the_rest_of_the_pass(string reason)
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: false, reason) };

        var pass = await Run(port, 150);

        _ = Assert.Single(port.Asked);   // no second pack, no split, no retry
        Assert.Equal(new LabelHow(0, FirstCandidate: 150, 0), pass.Value.Chosen);
        Assert.Equal(reason, pass.Value.PickUnavailable);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("no-key")]
    public async Task A_pick_that_was_never_in_force_is_not_a_degradation(string reason)
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: false, reason) };

        var pass = await Run(port, 3);

        _ = Assert.Single(port.Asked);
        Assert.Null(pass.Value.PickUnavailable);
        Assert.Equal(new LabelHow(0, FirstCandidate: 3, 0), pass.Value.Chosen);
    }

    [Fact]
    public async Task What_an_earlier_pack_decided_is_kept_when_a_later_one_cant_be_asked()
    {
        var calls = 0;
        var port = new FakeChoicePort { Script = qs => ++calls == 1 ? Second(qs) : ContextErrors.ChoiceUnavailable.With("network-error") };

        var pass = await Run(port, 150);

        Assert.Equal([100, 50], port.Asked.Select(a => a.Questions.Count));
        Assert.Equal(new LabelHow(Jev: 100, FirstCandidate: 50, 0), pass.Value.Chosen);
        Assert.Equal("network-error", pass.Value.PickUnavailable);
    }
}
