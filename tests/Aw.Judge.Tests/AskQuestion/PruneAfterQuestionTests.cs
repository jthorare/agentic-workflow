using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Features.AskQuestion;
using Aw.Judge.Testing;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.AskQuestion;

// Retention: after a question the judge drops the decision details older than 30 days before the start of today (UTC),
// so the cutoff moves once a day and the prune finds work at most once a day. Best effort: the answer never changes.
[Lane(TestLane.Unit)]
public sealed class PruneAfterQuestionTests
{
    private static readonly AskQuestionResponse _answer = new(new EscalatedOutcome("no-provider-decided"), ConfigWarning: null);

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddDays(100).AddHours(15));
    private readonly InMemoryDecisionLog _decisions = new();

    private DateTimeOffset Cutoff => new DateTimeOffset(_clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero) - TimeSpan.FromDays(30);

    [Fact]
    public async Task After_a_question_it_drops_the_details_older_than_30_days_before_the_start_of_today()
    {
        await Decision("old", Cutoff - TimeSpan.FromSeconds(1));
        await Decision("same-day", Cutoff + TimeSpan.FromSeconds(1));
        await Decision("new", _clock.GetUtcNow() - TimeSpan.FromDays(1));

        var result = await Decorated(Results.Success(_answer)).Handle(Request(), CancellationToken.None);

        Assert.Same(_answer, result.Value);
        Assert.Equal(["old:pruned", "same-day:kept", "new:kept"], _decisions.Decisions.Select(d => $"{d.Id.Value}:{(d.Details is null ? "pruned" : "kept")}").ToList());
    }

    [Fact]
    public async Task A_question_that_fails_prunes_nothing()
    {
        await Decision("old", Cutoff - TimeSpan.FromDays(1));

        var result = await Decorated(JudgeErrors.Invalid.With("unknown question: x")).Handle(Request(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.NotNull(Assert.Single(_decisions.Decisions).Details);
    }

    [Fact]
    public async Task A_store_that_cant_prune_changes_nothing_about_the_answer()
    {
        var decorated = new PruneAfterQuestion(new Answers(Results.Success(_answer)), new InMemoryDecisionLog { Unavailable = true }, _clock);

        var result = await decorated.Handle(Request(), CancellationToken.None);

        Assert.Same(_answer, result.Value);
    }

    private static AskQuestionRequest Request() => new("wake-gate", """{"text":"ok","senderKind":"main"}""");

    private PruneAfterQuestion Decorated(Result<AskQuestionResponse> answer) => new(new Answers(answer), _decisions, _clock);

    private async Task Decision(string id, DateTimeOffset at) =>
        Assert.True((await _decisions.Record(A.Decision().WithId(id).At(at).Build(), CancellationToken.None)).IsSuccess);

    private sealed class Answers(Result<AskQuestionResponse> answer) : IService<AskQuestionRequest, AskQuestionResponse>
    {
        public ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
