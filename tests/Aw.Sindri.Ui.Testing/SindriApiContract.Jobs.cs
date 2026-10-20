using Aw.Kernel;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the job calls (API P6) as the goldens serve them, so the fake and the real client both read what Sindri writes.</summary>
public abstract partial class SindriApiContract
{
    private const string JobId = "JOB-00000000000000000000000000000001";

    [Fact]
    public async Task Jobs_answers_every_golden_row()
    {
        var golden = GoldenFiles.Node("jobs", "running");

        var list = (await Reach(new FakeSindriApi()).Jobs(_api, new JobQuery(), Token)).Value;

        Expect.Sequence(golden["jobs"]!.AsArray().Select(j => (string)j!["id"]!), list.Jobs.Select(j => j.Id));
        var row = list.Jobs.Single();
        Expect.Equal(("EchoJob", "running", "service:host", null), (row.Kind, row.State, row.StartedBy, row.EndedAt));
    }

    [Fact]
    public async Task Jobs_empty_returns_the_served_sentence()
    {
        var server = new FakeSindriApi { JobsAnswer = RouteAnswer.Api("jobs", "empty") };

        var list = (await Reach(server).Jobs(_api, new JobQuery(), Token)).Value;

        Expect.Equal((string)GoldenFiles.Node("jobs", "empty")["empty"]!, list.Empty);
        Expect.Equal(0, list.Jobs.Count);
    }

    [Fact]
    public async Task Jobs_sends_its_filters_to_the_server()
    {
        var server = new FakeSindriApi { JobsAnswer = RouteAnswer.Api("jobs", "by-kind") };

        var list = (await Reach(server).Jobs(_api, new JobQuery(State: "succeeded", Kind: "RunService"), Token)).Value;

        Expect.Equal(new JobQuery("succeeded", "RunService"), server.LastJobsQuery);
        Expect.Equal(("RunService", "heartbeat", "ok: worked"), (list.Jobs[0].Kind, list.Jobs[0].Service, list.Jobs[0].Summary));
    }

    [Fact]
    public async Task Jobs_bad_kind_is_a_refusal_with_its_detail()
    {
        var server = new FakeSindriApi { JobsAnswer = RouteAnswer.Problem("jobs", "bad-kind") };

        var list = await Reach(server).Jobs(_api, new JobQuery(Kind: "NoSuchJob"), Token);

        Expect.Equal(new Error("SND-CLI-003", ErrorKind.Invalid, (string)GoldenFiles.Node("jobs", "bad-kind")["detail"]!), list.Error);
    }

    [Fact]
    public async Task Job_reads_the_progress_the_result_and_the_actions()
    {
        var server = new FakeSindriApi();
        server.JobAnswers[JobId] = RouteAnswer.Api("jobs-id", "context-backtest");
        var golden = GoldenFiles.Node("jobs-id", "context-backtest");

        var job = (await Reach(server).Job(_api, JobId, Token)).Value;

        Expect.Sequence(golden["progress"]!.AsArray().Select(p => (string)p!["line"]!), job.Progress.Select(p => p.Line));
        Expect.Equal(("RunContextBacktest", 2, "succeeded"), (job.Kind, job.Rounds, job.State));
        Expect.True(job.Result is not null);
        Expect.Equal(0, job.Actions.Count);
    }

    [Fact]
    public async Task A_running_job_names_the_cancel_and_its_cli()
    {
        var server = new FakeSindriApi();
        server.JobAnswers[JobId] = RouteAnswer.Api("jobs-id", "running");

        var job = (await Reach(server).Job(_api, JobId, Token)).Value;

        Expect.Sequence(["CancelJob"], job.Actions);
        Expect.Equal("sindri jobs cancel " + JobId, job.Cli);
    }

    [Fact]
    public async Task Job_unknown_id_is_the_404_refusal()
    {
        var result = await Reach(new FakeSindriApi()).Job(_api, JobId, Token);

        Expect.Equal(new Error("SND-JOB-404", ErrorKind.NotFound, (string)GoldenFiles.Node("jobs-id", "not-found")["detail"]!), result.Error);
    }

    [Fact]
    public async Task CancelJob_is_accepted_with_the_served_sequence_and_a_second_try_is_refused()
    {
        var server = new FakeSindriApi();

        var accepted = await Reach(server).CancelJob(_api, JobId, Token);
        server.CancelJobAnswer = RouteAnswer.Problem("jobs-id-cancel", "already-ended");
        var again = await Reach(server).CancelJob(_api, JobId, Token);

        Expect.Equal((long)GoldenFiles.Node("jobs-id-cancel", "accepted")["sequence"]!, accepted.Value.Sequence);
        Expect.Equal("SND-JOB-409", again.Error.Code);
        Expect.Sequence([JobId, JobId], server.CancelledJobs);
    }

    [Fact]
    public async Task CancelJob_by_a_session_is_the_403_refusal()
    {
        var server = new FakeSindriApi { CancelJobAnswer = RouteAnswer.Problem("jobs-id-cancel", "refused-session") };

        var result = await Reach(server).CancelJob(_api, JobId, Token);

        Expect.Equal(new Error("SND-AUTH-002", ErrorKind.Forbidden, (string)GoldenFiles.Node("jobs-id-cancel", "refused-session")["detail"]!), result.Error);
    }
}
