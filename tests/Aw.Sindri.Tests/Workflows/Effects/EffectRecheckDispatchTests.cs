using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// The recheck after the dispatch delay (engine spec rev 8.1 §5.7, §3.8, §4.3 W11, §10; engine P6 unit (b)), on the
/// time-skipping server with the shipped 'RecheckEffect': it reads the recipe's approval now and the approved profile's
/// 'workflow.effects' (an <see cref="InMemoryProfiles"/> stands in for the approved snapshot until unit (d) lands the
/// keys). A refusal writes 'outbox.refused' with its code and parks the instance with spec-conflict of that kind.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectRecheckDispatchTests(SindriTimeSkippingServer server, DevServer devServer) : IClassFixture<SindriTimeSkippingServer>, IClassFixture<DevServer>, IDisposable
{
    private static readonly TimeSpan _delay = EffectSettings.DefaultDispatchDelay;
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Revoked_effect_is_refused_at_dispatch_and_the_instance_parks_revoked_with_only_cancel()
    {
        var env = server.Environment;
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-91");
            await Armed(handle, timers: 1);
            _harness.Revoke(recipe);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            Assert.Equal(("SND-RECIPE-005", EffectState.Refused), (refused.Code, refused.State));
            Assert.Equal("spec-conflict (revoked)", parked.Detail);
            _ = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('e')), new WorkflowUpdateOptions(new string('e', 32))));
            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()), new WorkflowUpdateOptions(new string('c', 32)));
            Assert.Equal("cancelled", (await handle.GetResultAsync()).State);
            Assert.Equal("cancelled", cancelled.Instance.State);
            Assert.Empty(await EffectRows.Of(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            Assert.Empty(_desk.RecipeCalls);
        }, Token);
    }

    [Fact]
    public async Task An_effect_off_the_allowlist_parks_dispatch_and_resume_after_allowlisting_reemits_it()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow();
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-92");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            Assert.Equal(("SND-WF-011", "spec-conflict (dispatch)"), (refused.Code, parked.Detail));
            Assert.Empty(_desk.RecipeCalls);

            await Allow(_notify);
            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('b')), new WorkflowUpdateOptions(new string('b', 32)));
            await Armed(handle, timers: 2);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var dispatched = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            var pending = await EffectRows.Of(sindri, OutboxEvents.Pending, CommandOrigin.Recipe);

            Assert.Equal("sent", resumed.Instance.State);
            Assert.Equal(2, pending.Count);
            Assert.Equal(refused.Identity, dispatched.Identity);
            Assert.All(pending, p => Assert.Equal(refused.Identity, p.Identity));
            Assert.NotEqual(pending[0].Command, pending[1].Command, StringComparer.Ordinal);
            Assert.Equal(pending[1].Command, dispatched.Command);
            Assert.Equal([refused.Identity], _desk.RecipePerformed);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    [Fact]
    public async Task An_effect_whose_setup_cannot_run_parks_dispatch_naming_the_cause_without_retrying_forever()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.Failure = Core.Workflow.WorkflowErrors.NoRuntime.With("/repos/dev has no commit to point refs/sindri/me/T-95 at");
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-95");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            await env.DelayAsync(TimeSpan.FromMinutes(5), Token);

            Assert.Equal(("SND-WF-010", "spec-conflict (dispatch)"), (refused.Code, parked.Detail));
            Assert.Contains("has no commit", refused.Reason, StringComparison.Ordinal);
            Assert.Contains("has no commit", parked.Question?.Question, StringComparison.Ordinal);
            _ = Assert.Single(_desk.RecipeCalls);   // a non-retryable failure: one try, not the retry policy's
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>A block that replies an outcome no registered event declares can't fail the workflow: the effect is refused and the instance parks dispatch.</summary>
    [Fact]
    public async Task An_effect_replying_an_undeclared_outcome_is_refused_and_parks_dispatch_instead_of_failing_the_workflow()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.ReplyOutcome = "undeclared";
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-97");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            Assert.Equal(("SND-WF-015", "spec-conflict (dispatch)"), (refused.Code, parked.Detail));
            Assert.Contains("undeclared", refused.Reason, StringComparison.Ordinal);
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>An effect that keeps failing is tried the outbox policy's 8 times, then refused and parked (spec A-F20-F25), not retried forever.</summary>
    [Fact]
    public async Task An_effect_that_fails_every_attempt_is_refused_after_the_outbox_retry_policys_attempts()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.Failure = Core.Workflow.WorkflowErrors.TriggerNotAllowed.With("the desk is down");
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-98");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromHours(4), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));

            Assert.Equal("SND-WF-015", refused.Code);   // the state's 10 m timeout parked the instance meanwhile; the dispatch park waits behind it
            Assert.Equal(Kernel.Outbox.OutboxRetryPolicy.Default.MaxAttempts, _desk.RecipeCalls.Count);
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);

            // The refusal is held by a dispatch park behind the timeout park: resuming the timeout surfaces it, carrying the effect.
            _ = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('e')), new WorkflowUpdateOptions(new string('e', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "parked" && v.Detail == "spec-conflict (dispatch)");
            _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()), new WorkflowUpdateOptions(new string('c', 32)));   // ends the run cleanly: the shared server's next case must start with nothing open
            Assert.Equal("cancelled", (await handle.GetResultAsync()).State);
        }, Token);
    }

    /// <summary>rc2-2: a block that throws with a token-shaped message reaches the outbox row and the park question scrubbed (the activity-side scrub only covers a returned error).</summary>
    [Fact]
    public async Task A_block_that_throws_a_token_is_refused_and_parked_with_the_token_scrubbed()
    {
        const string token = "gh" + "p_0123456789abcdefghijABCDEFGHIJ012345";
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.Throw = new InvalidOperationException($"the desk rejected {token}");
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-99");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromHours(4), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            _ = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('e')), new WorkflowUpdateOptions(new string('e', 32)));   // the 10 m timeout park came first; resuming it surfaces the dispatch park
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked" && v.Detail == "spec-conflict (dispatch)");

            Assert.DoesNotContain(token, refused.Reason, StringComparison.Ordinal);
            Assert.Contains("[REDACTED:github-token]", refused.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain(token, parked.Question?.Question, StringComparison.Ordinal);
            Assert.Contains("[REDACTED:github-token]", parked.Question?.Question, StringComparison.Ordinal);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()), new WorkflowUpdateOptions(new string('c', 32)));
            Assert.Equal("cancelled", (await handle.GetResultAsync()).State);
        }, Token);
    }

    /// <summary>A cancel while the failing effect waits out its retry backoff stops the retries: no later send (PR 232 run 2, S1).</summary>
    [Fact]
    public async Task A_cancel_during_the_retry_backoff_sends_nothing_further()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.Failure = Core.Workflow.WorkflowErrors.TriggerNotAllowed.With("the desk is down");
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-97");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var tried = _desk.RecipeCalls.Count;
            Assert.InRange(tried, 1, Kernel.Outbox.OutboxRetryPolicy.Default.MaxAttempts - 1);   // one or more attempts failed; the next waits in its backoff

            _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", (await handle.GetResultAsync()).State);   // the time-skipping server runs the backoff out while the result is awaited
            Assert.Equal(tried, _desk.RecipeCalls.Count);
        }, Token);
    }

    /// <summary>A revoke after the delay but before the activity runs is still honoured: the run re-checks the approval.</summary>
    [Fact]
    public async Task RunEffect_refuses_when_the_recipe_was_revoked_after_the_delay()
    {
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _harness.Revoke(recipe);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var command = new EffectCommand(_notify, new EffectIdentity(WorkflowInstanceId.From("WFI-" + new string('8', 32)), Entry: 2, Index: 0), CommandOrigin.Recipe, new Dictionary<string, Core.Workflow.Recipes.WithValue>(StringComparer.Ordinal));
        var run = new EffectRun(InstanceAddress.Root(ItemKey.From("T-99"), RecipeId.From("workflow.dev-notify"), 1).Value, "T-99", Attempt: 1, command, "cmd-1", Text: null, new Dictionary<FactKey, FactValue>(), new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash));
        var regular = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { IsLocal = false } };

        var failure = await Assert.ThrowsAsync<ApplicationFailureException>(() => regular.RunAsync(() => sindri.GetRequiredService<EffectActivities>().RunAsync(run)));

        Assert.Equal(("SND-RECIPE-005", true), (failure.ErrorType, failure.NonRetryable));
        Assert.Empty(_desk.Calls);
    }

    /// <summary>The recheck now covers an engine-origin command too: only the desk notice may be one, whatever the instance's recipe.</summary>
    [Fact]
    public async Task An_engine_origin_command_other_than_the_desk_notice_is_refused_by_the_recheck()
    {
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await using var sindri = Sindri();
        var local = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { IsLocal = true } };

        var refused = await local.RunAsync(() => sindri.GetRequiredService<EffectActivities>().RecheckAsync(
            new EffectRecheck(new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash), BlockId.From("effect.claim.ref"), CommandOrigin.Engine)));

        Assert.Equal("SND-WF-011", refused.Code);
    }

    [Fact]
    public async Task A_drifted_recipes_effect_is_refused_and_the_instance_parks_drift()
    {
        var env = server.Environment;
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-93");
            await Armed(handle, timers: 1);
            await Drift(sindri, recipe);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            Assert.Equal(("SND-RECIPE-006", "spec-conflict (drift)"), (refused.Code, parked.Detail));
            Assert.Empty(_desk.RecipeCalls);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>The drift approval's wake reaches the parked instance through Temporal: the park ends and the refused effect is sent again (§3.8, §4.1).</summary>
    [Fact]
    public async Task A_drift_reapproval_wakes_the_parked_instance_and_resends_the_refused_effect()
    {
        var window = TimeSpan.FromSeconds(5);   // the profile's shortest dispatch delay: the dev server runs in real time, and the wake finds the instance through its visibility store, which the time-skipping server lacks
        var env = devServer.Environment;   // one real-time server for both wake cases
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow("5s", _notify);
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            _ = await WorkflowsHarness.AsServe(sindri, env, Token);
            var handle = await Completed(sindri, "T-96", env);
            await using var ended = new Terminating(handle);   // one failing case must not leave a running instance for the other on the shared server
            await Armed(handle, timers: 1, window);
            var original = await Pins(sindri, recipe);
            await Drift(sindri, recipe);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));   // polls until the delay's recheck refuses
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            _ = await Pins(sindri, recipe, original);   // the re-approval: the pins match this build again
            await Visible(sindri, recipe.Hash, handle);
            var woken = await sindri.GetRequiredService<IRecipeWake>().Wake(new RecipeWakeNotice(recipe.Hash, RecipeWakeKind.Approved, Sequence: 99, "human:cli"), Token);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "sent");
            await Armed(handle, timers: 2, window);
            var dispatched = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));   // polls until the delay passes

            Assert.True(woken.IsSuccess);
            Assert.Equal(refused.Identity, dispatched.Identity);
            Assert.Equal([refused.Identity], _desk.RecipePerformed);
        }, Token);
    }

    /// <summary>The revoke's wake reaches the instance through Temporal and parks it (revoked) at once, before the dispatch delay ends (§4.1, §3.8).</summary>
    [Fact]
    public async Task A_revoke_wakes_the_instance_into_a_revoked_park_before_the_dispatch_delay_ends()
    {
        var window = TimeSpan.FromSeconds(5);   // the profile's shortest dispatch delay: the dev server runs in real time
        var env = devServer.Environment;   // one real-time server for both wake cases
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow("5s", _notify);
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            _ = await WorkflowsHarness.AsServe(sindri, env, Token);
            var handle = await Completed(sindri, "T-101", env);
            await using var ended = new Terminating(handle);   // one failing case must not leave a running instance for the other on the shared server
            await Armed(handle, timers: 1, window);
            _harness.Revoke(recipe);
            await Visible(sindri, recipe.Hash, handle);
            var woken = await sindri.GetRequiredService<IRecipeWake>().Wake(new RecipeWakeNotice(recipe.Hash, RecipeWakeKind.Revoked, Sequence: 98, "human:cli"), Token);
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            var early = await EffectRows.Of(sindri, OutboxEvents.Refused, CommandOrigin.Recipe);   // the delay (5 s) is still running: no recheck has refused anything yet

            Assert.True(woken.IsSuccess);
            Assert.Equal("spec-conflict (revoked)", parked.Detail);
            Assert.Empty(early);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));   // the delay ends: the recheck refuses, and the park stays the revoke's
            var after = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            Assert.Equal(("SND-RECIPE-005", "spec-conflict (revoked)"), (refused.Code, after.Detail));
            Assert.Empty(_desk.RecipeCalls);
        }, Token);
    }

    /// <summary>Polls until the visibility store lists <paramref name="handle"/> among the recipe's running instances: the wake finds instances through it, so waking earlier races the indexing (and the shared server may list another case's).</summary>
    private static async Task Visible(IServiceProvider sindri, RecipeHash hash, WorkflowHandle handle)
    {
        var wake = new TemporalRecipeWake(sindri.GetRequiredService<TemporalFrontend>());
        var mine = WorkflowInstanceId.Of(InstanceAddress.From(handle.Id));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var running = await wake.Running(hash, Token);
            if (running.IsSuccess && running.Value.Contains(mine))
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "the visibility store never listed this test's instance");
            await Task.Delay(100, Token);
        }
    }

    /// <summary>Terminates the instance when the case ends, whatever its outcome; an instance that already ended is left alone.</summary>
    private sealed class Terminating(WorkflowHandle handle) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await handle.TerminateAsync("test over");
            }
            catch (RpcException)
            {
                // already closed: nothing to stop
            }
        }
    }

    /// <summary>The pins of the latest approval of <paramref name="recipe"/>; with <paramref name="set"/>, a newer approval carrying those pins.</summary>
    private static async Task<RecipePins> Pins(IServiceProvider sindri, RegisteredRecipe recipe, RecipePins? set = null)
    {
        var approvals = sindri.GetRequiredService<IRecipeApprovals>();
        var latest = (await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value!;
        if (set is { } pins)
        {
            var approved = await approvals.Approve(latest with { Pins = pins, ApprovedAt = latest.ApprovedAt.AddMinutes(1) }, Token);
            Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        }

        return latest.Pins;
    }

    [Fact]
    public async Task The_engines_desk_notice_is_never_refused_even_on_a_revoked_recipe_without_an_allowlist()
    {
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        _harness.Revoke(recipe);
        await using var sindri = Sindri();
        var version = new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash);
        var local = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { IsLocal = true } };
        var activities = sindri.GetRequiredService<EffectActivities>();

        var engine = await local.RunAsync(() => activities.RecheckAsync(new EffectRecheck(version, _notify, CommandOrigin.Engine)));
        var fromRecipe = await local.RunAsync(() => activities.RecheckAsync(new EffectRecheck(version, _notify, CommandOrigin.Recipe)));

        Assert.Null(engine.Code);
        Assert.Equal("SND-RECIPE-005", fromRecipe.Code);
    }

    /// <summary>The host over the harness home, with the fake desk block and <see cref="_profiles"/> as the approved profile.</summary>
    private ServiceProvider Sindri(WorkflowEnvironment? on = null) => _harness.Sindri(
        services =>
        {
            _desk.Replace(services);
            _ = services.AddSingleton<IApprovedProfile>(_profiles);
        },
        name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? (on ?? server.Environment).Client.Options.Namespace : null);   // the registry's wake signals in the server's namespace

    /// <summary>Approves a profile whose 'workflow.effects' lists <paramref name="kinds"/> (none: no allowlist at all).</summary>
    private Task Allow(params BlockId[] kinds) => Allow(delay: null, kinds);

    private async Task Allow(string? delay, params BlockId[] kinds)
    {
        var workflow = kinds.Length == 0 ? null : new WorkflowSettings(Effects: [.. kinds.Select(static k => k.Value)], DispatchDelay: delay);
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: workflow);
        var saved = await _profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string((char)('a' + kinds.Length), 64)), "/home/profile.yaml"), Token);
        Assert.False(saved.IsError);
    }

    /// <summary>A drift approval of <paramref name="recipe"/>'s hash whose block pins no longer match this build (§4.1).</summary>
    private static async Task Drift(IServiceProvider sindri, RegisteredRecipe recipe)
    {
        var approvals = sindri.GetRequiredService<IRecipeApprovals>();
        var latest = (await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value!; // why: the harness approved it
        var stale = new BlockPin(_notify, DescriptorHash.From("sha256:" + new string('0', 64)));
        var pins = latest.Pins with { Blocks = [.. latest.Pins.Blocks.Select(p => p.Block == _notify ? stale : p)] };
        var drifted = await approvals.Approve(latest with { Pins = pins, ApprovedAt = latest.ApprovedAt.AddMinutes(1) }, Token);
        Assert.False(drifted.IsError, drifted.IsError ? drifted.Error.Message : null);
    }

    /// <summary>Starts 'workflow.dev-notify' on <paramref name="item"/> and completes its desk Step: 'ask -> sent' with 'effect.notify.desk'.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Completed(IServiceProvider sindri, string item, WorkflowEnvironment? on = null)
    {
        var handle = await WorkflowsHarness.Start((on ?? server.Environment).Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", item, Token));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
        var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
            new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = _harness.Fact("Hello.") }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
        var completed = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('9', 32)));
        Assert.Equal("-> sent", completed.Happened);
        return handle;
    }

    /// <summary>Waits until the instance's history has <paramref name="timers"/> dispatch-delay timers (each follows a pending row).</summary>
    private static Task Armed(WorkflowHandle handle, int timers) => Armed(handle, timers, _delay);

    private static async Task Armed(WorkflowHandle handle, int timers, TimeSpan delay)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var started = (await handle.FetchHistoryAsync()).Events
                .Count(e => e.EventType == EventType.TimerStarted && e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() == delay);
            if (started >= timers)
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the dispatch delay's timer {timers} was never started"));
    }

    private static AnswerGiven Resume(char nonce) =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string(nonce, 32)), new string('0', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static CancelRequested Cancel() =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('c', 32)), new string('0', 64)), Sequence: null, Parent: null);
}
