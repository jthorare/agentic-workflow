using System.Net;
using System.Text.Json;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Jobs;
using static Aw.Sindri.Tests.ApiGoldens.ApproveAllScenarios;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The approve-all routes on the real host (setup-once spec §5.3, §7.2, §7.4): the plan, the hold as a job, and every refusal around it.</summary>
public sealed class ApproveAllRoutesTests
{
    private static string Terminal(GoldenHost host) => host.Token.Value;

    private static string Code(Answer answer) => answer.Code ?? string.Empty;

    private static async Task<GoldenHost> Started(FakeShippedRecipes shipped, CancellationToken ct, bool approveProfile = true)
    {
        var host = await Start(shipped, ct);
        if (approveProfile)
        {
            await ViewScenarios.Approve(host, ct);
        }

        return host;
    }

    private static async Task<JsonElement> Job(GoldenHost host, string job) =>
        JsonDocument.Parse((await JobHost.Read(host, "/sindri/jobs/" + job)).Body).RootElement;

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Before_a_profile_is_approved_the_plan_and_the_hold_are_conflicts()
    {
        await using var host = await Started(new FakeShippedRecipes(ApproveAllFixtures.StepAndItem()), TestContext.Current.CancellationToken, approveProfile: false);

        var plan = await host.Http.Ask(Get(PlanRoute, Human(Terminal(host))));
        var challenge = await host.Http.Ask(Post("/sindri/workflow/challenges", """{"action":"approve-all c41e07"}""", Human(Terminal(host))));
        var post = await host.Http.Ask(Post(PlanRoute, """{"challenge":"CHL-00000000000000000000000000000000","typed":"approve-all c41e07"}""", Human(Terminal(host))));

        Assert.Equal([(HttpStatusCode.Conflict, "SND-SETUP-007")], new[] { plan, challenge, post }.Select(static a => (a.Status, Code(a))).Distinct().ToArray());
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task One_hold_starts_a_job_that_approves_the_plan_and_the_plan_is_then_empty()
    {
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await Started(shipped, TestContext.Current.CancellationToken);

        var job = await Hold(host, TestContext.Current.CancellationToken);
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var view = await Job(host, job);
        var after = await host.Http.Ask(Get(PlanRoute, Human(Terminal(host))));

        Assert.Equal(("succeeded", "ApproveAllRecipes", "approve-all", "2 approved, 0 refused, 0 skipped"),
            (view.GetProperty("state").GetString(), view.GetProperty("kind").GetString(), view.GetProperty("key").GetString(), view.GetProperty("summary").GetString()));
        Assert.Equal(["One step", "Work item"], view.GetProperty("progress").EnumerateArray().Select(static p => p.GetProperty("line").GetString()).ToArray(), StringComparer.Ordinal);
        Assert.Equal([ApproveAllFixtures.Step, ApproveAllFixtures.Item], shipped.Approved.Select(static a => a.Id));
        Assert.Equal("Every recipe is approved.", JsonDocument.Parse(after.Body).RootElement.GetProperty("empty").GetString(), StringComparer.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_second_hold_while_one_runs_is_a_409_naming_the_running_job()
    {
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem()) { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var host = await Started(shipped, TestContext.Current.CancellationToken);
        var running = await Hold(host, TestContext.Current.CancellationToken);
        var second = await Answer(host, await Digest(host));

        var refused = await host.Http.Ask(Post(PlanRoute, second, Human(Terminal(host))));
        shipped.Hold.SetResult();
        await JobHost.Settle(host, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Conflict, "SND-JOB-409"), (refused.Status, Code(refused)));
        Assert.Equal(running, JsonDocument.Parse(refused.Body).RootElement.GetProperty("runningJob").GetString(), StringComparer.Ordinal);
        Assert.Equal(1, shipped.Applied);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task An_empty_plan_answers_200_with_an_empty_result_and_starts_no_job()
    {
        await using var host = await Started(new FakeShippedRecipes([]), TestContext.Current.CancellationToken);

        var posted = await host.Http.Ask(Post(PlanRoute, json: null, Human(Terminal(host))));
        var jobs = await JobHost.Read(host, "/sindri/jobs");

        Assert.Equal(HttpStatusCode.OK, posted.Status);
        Assert.Equal("""{"approved":[],"refused":[],"skipped":[]}""", posted.Body);
        Assert.Empty(JsonDocument.Parse(jobs.Body).RootElement.GetProperty("jobs").EnumerateArray());
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_plan_with_only_a_recipe_needing_its_own_approval_answers_200_empty_and_starts_no_job()
    {
        var shipped = new FakeShippedRecipes([]);
        shipped.OwnApproval.Add(ApproveAllFixtures.ShipNeedsItsOwn());
        await using var host = await Started(shipped, TestContext.Current.CancellationToken);

        var posted = await host.Http.Ask(Post(PlanRoute, json: null, Human(Terminal(host))));
        var jobs = await JobHost.Read(host, "/sindri/jobs");

        Assert.Equal((HttpStatusCode.OK, """{"approved":[],"refused":[],"skipped":[]}"""), (posted.Status, posted.Body));
        Assert.Empty(JsonDocument.Parse(jobs.Body).RootElement.GetProperty("jobs").EnumerateArray());
        Assert.Equal(0, shipped.Applied);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_route_is_not_read_as_a_recipe_id_and_hash()
    {
        await using var host = await Started(new FakeShippedRecipes(ApproveAllFixtures.StepAndItem()), TestContext.Current.CancellationToken);

        var plan = await host.Http.Ask(Get(PlanRoute, Human(Terminal(host))));
        var show = await host.Http.Ask(Get("/sindri/workflow/recipes/workflow.step@" + new string('a', 12), Human(Terminal(host))));

        Assert.Equal(HttpStatusCode.OK, plan.Status);
        Assert.NotEqual(HttpStatusCode.OK, show.Status);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_hold_with_a_refusal_keeps_the_rest_and_names_each_in_the_result()
    {
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.OneRefused());
        await using var host = await Started(shipped, TestContext.Current.CancellationToken);

        var job = await Hold(host, TestContext.Current.CancellationToken);
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var result = (await Job(host, job)).GetProperty("result");

        Assert.Equal(["workflow.step", "workflow.note"], result.GetProperty("approved").EnumerateArray().Select(static a => a.GetProperty("id").GetString()).ToArray(), StringComparer.Ordinal);
        Assert.Equal(["workflow.item"], result.GetProperty("refused").EnumerateArray().Select(static a => a.GetProperty("id").GetString()).ToArray(), StringComparer.Ordinal);
        Assert.Equal([("workflow.review", "workflow.item")], result.GetProperty("skipped").EnumerateArray().Select(static a => (a.GetProperty("id").GetString(), a.GetProperty("because").GetString())));
    }
}
