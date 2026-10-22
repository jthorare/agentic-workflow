using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri repo add' over a scripted API (setup-once spec §7.4): the parse, the absolute path, the body it posts, --detach, the job it follows and the line it prints at the end.</summary>
public sealed class RepoVerbsTests : IDisposable
{
    private const string StartedBody = """{"accepted":{"commandId":"00000000-0000-0000-0000-00000000000c","sequence":5},"jobId":"JOB-0000000000000000000000000000000e","links":{"job":"/sindri/jobs/JOB-0000000000000000000000000000000e","cancel":"/sindri/jobs/JOB-0000000000000000000000000000000e/cancel","events":"/sindri/events?after=4&types=job.*"}}""";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly JobRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _rig.Dispose();
    }

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        return await new RepoVerbs(client, output, new JobFollower(client, output, TimeProvider.System)).Run(args, Token, Token);
    }

    private static string BodyPath(ScriptedApi api) => JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement.GetProperty("path").GetString()!;

    [Fact, Trait("Lane", "unit")]
    public async Task Add_posts_the_absolute_path_to_the_route_and_with_detach_prints_the_job()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, StartedBody));

        var exit = await Run(api, "add", "/work/demo", "--detach");

        Assert.True(exit == 0, _error.ToString());
        var (method, route, body) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/setup/repos"), (method, route));
        Assert.Equal("/work/demo", JsonDocument.Parse(body).RootElement.GetProperty("path").GetString());
        Assert.False(JsonDocument.Parse(body).RootElement.TryGetProperty("name", out var name) && name.ValueKind != JsonValueKind.Null);
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relative_path_is_made_absolute_from_the_working_directory()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, StartedBody));

        _ = await Run(api, "add", "some/repo", "--detach");

        Assert.Equal(Path.GetFullPath("some/repo"), BodyPath(api));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tilde_path_is_the_users_home()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, StartedBody));

        _ = await Run(api, "add", "~/work/demo", "--detach");

        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/work/demo"), BodyPath(api));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Name_and_flags_may_come_in_any_order()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, StartedBody));

        var exit = await Run(api, "add", "--name", "my-demo", "--json", "/work/demo", "--detach");

        Assert.Equal(0, exit);
        var sent = JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement;
        Assert.Equal(("/work/demo", "my-demo"), (sent.GetProperty("path").GetString(), sent.GetProperty("name").GetString()));
        Assert.Contains("\"jobId\":\"JOB-0000000000000000000000000000000e\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("add")]
    [InlineData("add", "/a", "/b")]
    [InlineData("add", "/a", "--bogus")]
    [InlineData("add", "/a", "--name")]
    [InlineData("remove", "/a")]
    public async Task Anything_else_is_the_usage_error_and_posts_nothing(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, StartedBody));

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains(RepoVerbs.Usage, _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_start_is_exit_2_with_the_code()
    {
        var api = new ScriptedApi((HttpStatusCode.Conflict, """{"type":"x","title":"SND-SETUP-002","status":409,"detail":"that folder is already the repo demo (fix: pick another folder)"}"""));

        var exit = await Run(api, "add", "/work/demo");

        Assert.Equal(2, exit);
        Assert.Contains("SND-SETUP-002", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_detach_the_verb_follows_the_job_and_says_the_profile_waits_for_approval()
    {
        var result = JsonDocument.Parse("""{"repo":"demo","created":true}""").RootElement.Clone();
        var api = new JobApi().Ok("POST", "/sindri/setup/repos", Body(Started)).Ok("GET", "/sindri/jobs/" + Id, View(JobState.Succeeded, rounds: 2, summary: "added demo", result: result));
        var verbs = new RepoVerbs(Client(api), _rig.Output(), _rig.Follower(api));
        var run = Task.Run(() => verbs.Run(["add", "/work/demo"], _rig.Interrupted, _rig.Again), Token);
        await api.Until(() => api.Streams == 1);

        await api.Frame(11, "job.progress", Progress(1, "checking"));
        await api.Frame(12, "job.finished", Finished("succeeded", "added demo"));

        Assert.Equal(0, await Finished(run));
        var text = _rig.Out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("checking\n", text, StringComparison.Ordinal);
        Assert.Contains("succeeded: added demo\n", text, StringComparison.Ordinal);
        Assert.Contains("added demo; your profile waits for your approval: sindri profile approve\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_job_is_exit_1()
    {
        var api = new JobApi().Ok("POST", "/sindri/setup/repos", Body(Started)).Ok("GET", "/sindri/jobs/" + Id, View(JobState.Failed, rounds: 1, summary: "the profile changed", code: "SND-PROFILE-002"));
        var verbs = new RepoVerbs(Client(api), _rig.Output(), _rig.Follower(api));
        var run = Task.Run(() => verbs.Run(["add", "/work/demo"], _rig.Interrupted, _rig.Again), Token);
        await api.Until(() => api.Streams == 1);

        await api.Frame(11, "job.finished", Finished("failed", "the profile changed", "SND-PROFILE-002"));

        Assert.Equal(1, await Finished(run));
    }
}
