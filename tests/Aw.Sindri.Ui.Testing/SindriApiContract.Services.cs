using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the services rows and the service and stop commands, as the API's goldens serve them.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task Services_answers_each_scheduled_service_with_its_last_run()
    {
        var server = new FakeSindriApi { ServiceView = RouteAnswer.Api("services", "ran-failed") };

        var rows = (await Reach(server).Services(_api, Token)).Value.Services!;

        var heartbeat = rows.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal));
        Expect.Equal(("5s", false, "failed", "SND-SCHED-001", null), (heartbeat.Every, heartbeat.Paused, heartbeat.LastRun!.Outcome, heartbeat.LastRun.Code, heartbeat.Job));
        Expect.Equal(GoldenFiles.Node("services", "ran-failed")["services"]!.AsArray().Count, rows.Count);
    }

    [Fact]
    public async Task Services_answers_a_paused_service_and_the_job_of_a_run_in_flight()
    {
        var paused = (await Reach(new FakeSindriApi { ServiceView = RouteAnswer.Api("services", "paused") }).Services(_api, Token)).Value.Services!;
        var running = (await Reach(new FakeSindriApi { ServiceView = RouteAnswer.Api("services", "running-job") }).Services(_api, Token)).Value.Services!;

        Expect.True(paused.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal)).Paused);
        Expect.Equal((string)GoldenFiles.Node("services", "running-job")["services"]!.AsArray().Single(r => (string?)r!["job"] is not null)!["job"]!, running.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal)).Job);
    }

    [Fact]
    public async Task RunService_answers_the_started_job_and_names_the_service()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).RunService(_api, "stuck", Token)).Value;

        Expect.Equal(((string)GoldenFiles.Node("services-name-run", "started")["jobId"]!, (long)GoldenFiles.Node("services-name-run", "started")["accepted"]!["sequence"]!), (started.JobId, started.Accepted.Sequence));
        Expect.Sequence(["stuck"], server.RunServiceCalls);
    }

    [Fact]
    public async Task RunService_while_one_runs_is_a_conflict_with_the_running_job()
    {
        var server = new FakeSindriApi { RunServiceAnswer = RouteAnswer.Problem("services-name-run", "already-running") };

        var result = await Reach(server).RunService(_api, "stuck", Token);

        Expect.Equal(("SND-JOB-409", ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
        Expect.Equal((string)GoldenFiles.Node("services-name-run", "already-running")["runningJob"]!, result.Error.Extensions?.FirstOrDefault(e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());
    }

    [Fact]
    public async Task PauseService_and_ResumeService_answer_the_accepted_command_and_name_the_service()
    {
        var server = new FakeSindriApi();

        var paused = (await Reach(server).PauseService(_api, "heartbeat", Token)).Value;
        var resumed = (await Reach(server).ResumeService(_api, "heartbeat", Token)).Value;

        Expect.Equal((long)GoldenFiles.Node("services-name-pause", "accepted")["sequence"]!, paused.Sequence);
        Expect.Equal((long)GoldenFiles.Node("services-name-resume", "accepted")["sequence"]!, resumed.Sequence);
        Expect.Sequence(["heartbeat"], server.PauseServiceCalls);
        Expect.Sequence(["heartbeat"], server.ResumeServiceCalls);
    }

    [Fact]
    public async Task PauseService_of_an_unknown_service_is_not_found_and_an_agent_session_is_refused()
    {
        var unknown = await Reach(new FakeSindriApi { PauseServiceAnswer = RouteAnswer.Problem("services-name-pause", "unknown-service") }).PauseService(_api, "ghost", Token);
        var session = await Reach(new FakeSindriApi { PauseServiceAnswer = RouteAnswer.Problem("services-name-pause", "refused-session") }).PauseService(_api, "heartbeat", Token);

        Expect.Equal(("SND-SVC-404", ErrorKind.NotFound), (unknown.Error.Code, unknown.Error.Kind));
        Expect.Equal(("SND-AUTH-002", ErrorKind.Forbidden), (session.Error.Code, session.Error.Kind));
    }

    [Fact]
    public async Task StopSindri_answers_the_process_told_to_stop()
    {
        var server = new FakeSindriApi();

        var stopped = (await Reach(server).StopSindri(_api, Token)).Value;

        Expect.Equal((4242, 1L, 2L), (stopped.Pid, stopped.Epoch, stopped.Sequence));
        Expect.Equal(1, server.StopCalls);
    }

    [Fact]
    public async Task StopSindri_is_refused_to_an_agent_session()
    {
        var server = new FakeSindriApi { StopAnswer = RouteAnswer.Problem("stop", "refused-session") };

        var result = await Reach(server).StopSindri(_api, Token);

        Expect.Equal(("SND-AUTH-002", ErrorKind.Forbidden), (result.Error.Code, result.Error.Kind));
    }
}
