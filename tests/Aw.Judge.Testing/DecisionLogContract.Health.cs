using System.Globalization;
using Aw.Judge.Core.Decisions;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IDecisionLog contract: the health window, the last attempts, pruning and an unusable store.</summary>
public abstract partial class DecisionLogContract
{
    /// <summary>Failures count from the start of the window inclusive; older ones don't.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Failures_count_from_the_start_of_the_window_inclusive()
    {
        var log = await Create(_none);
        var since = KnownInstants.Epoch;
        foreach (var ts in new[] { since.AddTicks(-1), since, since.AddHours(3) })
        {
            var recorded = await log.RecordFailure(new FailureRecord(ts, "wake-gate", "claude-cli", "timeout"), _none);
            Expect.That(recorded.IsSuccess, "a failure is recorded");
        }

        var count = await log.CountFailuresSince(since, _none);

        Expect.That(count.IsSuccess && count.Value == 2, "the failure at the start and the later one count; the one a tick before doesn't");
    }

    /// <summary>The last attempts are newest first, skip escalated rows and stop at the count asked for.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Last_attempts_are_newest_first_without_escalated_rows()
    {
        var log = await Create(_none);
        var outcomes = new[]
        {
            DecisionOutcome.Failed, DecisionOutcome.Decided, DecisionOutcome.Escalated, DecisionOutcome.Failed, DecisionOutcome.Escalated,
        };
        for (var i = 0; i < outcomes.Length; i++)
        {
            var decision = A.Decision().WithId("d" + i.ToString(CultureInfo.InvariantCulture)).At(KnownInstants.Epoch.AddMinutes(i)).WithOutcome(outcomes[i]).Build();
            Expect.That((await log.Record(decision, _none)).IsSuccess, "a decision is recorded");
        }

        var last = await log.LastAttemptOutcomes(2, _none);
        var all = await log.LastAttemptOutcomes(10, _none);

        Expect.That(last.IsSuccess && last.Value.SequenceEqual([DecisionOutcome.Failed, DecisionOutcome.Decided]), "the two newest attempts, escalated rows skipped");
        Expect.That(all.IsSuccess && all.Value.SequenceEqual([DecisionOutcome.Failed, DecisionOutcome.Decided, DecisionOutcome.Failed]), "every attempt, newest first");
    }

    /// <summary>A pre-rule settlement never reached a provider, so it isn't a model attempt: failures around it still make a streak.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pre_rule_settlements_are_not_model_attempts()
    {
        var log = await Create(_none);
        for (var i = 0; i < 10; i++)
        {
            var builder = A.Decision().WithId("d" + i.ToString(CultureInfo.InvariantCulture)).At(KnownInstants.Epoch.AddMinutes(i));
            var decision = (i % 2 == 0 ? builder.WithOutcome(DecisionOutcome.Failed) : builder.SettledByPreRule()).Build();
            Expect.That((await log.Record(decision, _none)).IsSuccess, "a decision is recorded");
        }

        var last = await log.LastAttemptOutcomes(5, _none);

        Expect.That(last.IsSuccess && last.Value.Count == 5 && last.Value.All(static o => o == DecisionOutcome.Failed), "five failed attempts, the pre-rule rows between them skipped");
    }

    /// <summary>A rule-timeout escalation never reached a provider either: a run of them doesn't make a 'down' streak.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rule_timeout_escalations_are_not_model_attempts()
    {
        var log = await Create(_none);
        for (var i = 0; i < 10; i++)
        {
            var builder = A.Decision().WithId("d" + i.ToString(CultureInfo.InvariantCulture)).At(KnownInstants.Epoch.AddMinutes(i));
            var decision = (i % 2 == 0 ? builder.WithOutcome(DecisionOutcome.Decided) : builder.RuleTimedOut()).Build();
            Expect.That((await log.Record(decision, _none)).IsSuccess, "a decision is recorded");
        }

        var last = await log.LastAttemptOutcomes(5, _none);

        Expect.That(last.IsSuccess && last.Value.Count == 5 && last.Value.All(static o => o == DecisionOutcome.Decided), "five decided attempts, the rule-timeout rows between them skipped");
    }

    /// <summary>An empty log has no failures and no attempts.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_log_has_no_failures_and_no_attempts()
    {
        var log = await Create(_none);

        var failures = await log.CountFailuresSince(KnownInstants.Epoch, _none);
        var attempts = await log.LastAttemptOutcomes(5, _none);

        Expect.That(failures.IsSuccess && failures.Value == 0, "no failures");
        Expect.That(attempts.IsSuccess && attempts.Value.Count == 0, "no attempts");
    }

    /// <summary>Pruning deletes only the detail rows of decisions made before the cut-off; every decision stays.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pruning_deletes_only_the_details_of_older_decisions()
    {
        var log = await Create(_none);
        var cutoff = KnownInstants.Epoch.AddDays(30);
        var old = A.Decision().WithId("old").At(cutoff.AddTicks(-1)).Build();
        var oldBare = A.Decision().WithId("old-bare").At(cutoff.AddDays(-1)).WithoutDetails().Build();
        var edge = A.Decision().WithId("edge").At(cutoff).Build();
        var recent = A.Decision().WithId("recent").At(cutoff.AddDays(1)).Build();
        foreach (var decision in new[] { old, oldBare, edge, recent })
        {
            Expect.That((await log.Record(decision, _none)).IsSuccess, "a decision is recorded");
        }

        var pruned = await log.PruneDetailsBefore(cutoff, _none);

        Expect.That(pruned.IsSuccess && pruned.Value == 1, "one detail row (the older decision's) is deleted");
        foreach (var expected in new[] { old with { Details = null }, oldBare, edge, recent })
        {
            var found = await log.Find(expected.Id, _none);
            Expect.That(found.IsSuccess && DecisionEquality.Same(expected, found.Value), "every decision stays; only the older one lost its details");
        }
    }

    /// <summary>A store that can't be opened returns errors and never throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unusable_store_returns_errors()
    {
        var log = await CreateUnavailable(_none);
        var decision = A.Decision().Build();

        Expect.That((await log.Record(decision, _none)).IsError, "Record returns an error");
        Expect.That((await log.RecordFailure(new FailureRecord(KnownInstants.Epoch, "wake-gate", "claude-cli", "timeout"), _none)).IsError, "RecordFailure returns an error");
        Expect.That((await log.Find(decision.Id, _none)).IsError, "Find returns an error");
        Expect.That((await log.CountFailuresSince(KnownInstants.Epoch, _none)).IsError, "CountFailuresSince returns an error");
        Expect.That((await log.LastAttemptOutcomes(5, _none)).IsError, "LastAttemptOutcomes returns an error");
    }
}
