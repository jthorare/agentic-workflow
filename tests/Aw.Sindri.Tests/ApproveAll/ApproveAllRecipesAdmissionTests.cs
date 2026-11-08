using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>
/// What an approve-all start checks inside the gate, in the spec's order (setup-once spec §5.3): the profile, a running approve-all, then the plan is kept for the job (no typed challenge: owner
/// decision q93); nothing is written by the checks.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllRecipesAdmissionTests : IDisposable
{
    private readonly SliceHarness _slice = new();
    private readonly FakeShippedRecipes _shipped = new(StepAndItem());
    private readonly JobRegistry _jobs = new();
    private readonly RedeemedPlans _kept = new();

    private IDisposable? _caller;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _caller?.Dispose();

    private ApproveAllRecipesAdmission Admission()
    {
        _caller ??= _slice.Callers.EnterService("test");
        return new(new ApproveAllPlanReader(_slice.Profiles, _shipped), _jobs, _kept, _slice.Callers);
    }

    private async Task Approved() => _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);

    [Fact]
    public async Task With_no_profile_the_start_is_refused_and_the_hold_is_not_burned()
    {
        var admitted = await Admission().Admit(new ApproveAllRecipesRequest(), Token);

        Assert.Equal("SND-PROFILE-001", admitted.Error.Code);
    }

    [Fact]
    public async Task A_running_approve_all_is_a_conflict_that_names_it()
    {
        await Approved();
        var running = JobId.From("JOB-" + new string('a', 32));
        Assert.True(_jobs.TryClaim(ApproveAllRecipesAdmission.Kind, ApproveAllRecipesAdmission.Key, running, out _));

        var admitted = await Admission().Admit(new ApproveAllRecipesRequest(), Token);

        Assert.Equal("SND-JOB-409", admitted.Error.Code);
        Assert.Equal(running.Value, admitted.Error.Extensions!.Single(static e => e.Key == "runningJob").Value.ToString()!.Trim('"'));
    }

    [Fact]
    public async Task The_start_keeps_the_plan_for_the_job_and_writes_nothing()
    {
        await Approved();
        var request = new ApproveAllRecipesRequest();
        var ledgerBefore = _slice.Store.Events.Count;

        var admitted = await Admission().Admit(request, Token);

        Assert.True(admitted.IsSuccess);
        Assert.True(_kept.Take(request, out var plan));
        Assert.Equal([Step, Item], plan!.Plan.Recipes.Select(static r => r.Id));
        Assert.Equal(_slice.Callers.Current!.CommandId, plan.Batch);   // why: the admission ran under the caller entered above
        Assert.Equal(ledgerBefore, _slice.Store.Events.Count);
        Assert.Equal(0, _shipped.Applied);
    }

    [Fact]
    public async Task A_rolled_back_start_gives_the_plan_up()
    {
        await Approved();
        var admission = Admission();
        var request = new ApproveAllRecipesRequest();
        _ = await admission.Admit(request, Token);

        admission.Rollback(request);

        Assert.False(_kept.Take(request, out _));
    }
}
