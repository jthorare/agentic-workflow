using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the map refresh's start, as the spec-shaped goldens serve it (the API has none yet).</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task RefreshContextMap_answers_the_started_job()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).RefreshContextMap(_api, Token)).Value;

        Expect.Equal(("JOB-000000000000000000000000000000a1", 21L), (started.JobId, started.Accepted.Sequence));
        Expect.Equal(1, server.RefreshContextMapCalls);
    }

    [Fact]
    public async Task RefreshContextMap_while_one_runs_is_a_conflict_with_the_job_code()
    {
        var server = new FakeSindriApi { RefreshContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-running") };

        var result = await Reach(server).RefreshContextMap(_api, Token);

        Expect.Equal(("SND-JOB-409", ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
        Expect.Equal("JOB-000000000000000000000000000000a0", result.Error.Extensions?.FirstOrDefault(e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());   // the running job rides on the refusal
    }

    [Fact]
    public async Task RefreshContextMap_409_without_a_running_job_carries_none()
    {
        var server = new FakeSindriApi { RefreshContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-ended") };

        var result = await Reach(server).RefreshContextMap(_api, Token);

        Expect.Equal("SND-JOB-409", result.Error.Code);
        Expect.Equal(0, result.Error.Extensions?.Count(e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)) ?? 0);
    }

    [Fact]
    public async Task BuildContextMap_answers_the_started_job()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).BuildContextMap(_api, Token)).Value;

        Expect.Equal(("JOB-000000000000000000000000000000a1", 21L), (started.JobId, started.Accepted.Sequence));
        Expect.Equal(1, server.BuildContextMapCalls);
    }

    [Fact]
    public async Task BuildContextMap_while_one_runs_is_a_conflict_carrying_the_running_job()
    {
        var server = new FakeSindriApi { BuildContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-running") };

        var result = await Reach(server).BuildContextMap(_api, Token);

        Expect.Equal(("SND-JOB-409", ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
        Expect.Equal("JOB-000000000000000000000000000000a0", result.Error.Extensions?.FirstOrDefault(e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());
    }
}
