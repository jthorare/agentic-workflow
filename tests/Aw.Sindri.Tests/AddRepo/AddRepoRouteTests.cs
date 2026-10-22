using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>POST /sindri/setup/repos over the served pipeline (setup-once spec §5.1): the job runs to the end, writes the profile file and records 'repo.added' with no path in its public form.</summary>
[Lane(TestLane.Integration)]
public sealed class AddRepoRouteTests : IAsyncLifetime
{
    private GoldenHost? _host;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GoldenHost Host => _host ?? throw new InvalidOperationException("not started");

    private string ProfilePath => Path.Combine(Host.Home, "profile", "profile.yaml");

    public async ValueTask InitializeAsync() =>
        _host = await GoldenHost.Start(temporary: true, Token, extra: services => services.Replace(ServiceDescriptor.Singleton<IRepoInspector>(SetupRepoScenarios.Inspector())));

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> Add(string json)
    {
        var answer = await Host.Http.Ask(Post("/sindri/setup/repos", json, Human(Host.Token.Value)));
        return (answer.Status, answer.Body);
    }

    private async Task<JsonElement> Finished(string started)
    {
        var job = JsonDocument.Parse(started).RootElement.GetProperty("jobId").GetString();
        for (var i = 0; i < 200; i++)
        {
            await ViewScenarios.CatchUp(Host, Token);
            var body = (await Host.Http.Ask(Get($"/sindri/jobs/{job}", Human(Host.Token.Value)))).Body;
            var view = JsonDocument.Parse(body).RootElement.Clone();
            if (view.GetProperty("state").GetString() != "running")
            {
                return view;
            }

            await Task.Delay(50, Token);
        }

        throw new TimeoutException("the AddRepo job did not end");
    }

    [Fact]
    public async Task A_good_folder_starts_a_job_and_the_job_adds_it_to_the_profile_and_records_repo_added()
    {
        var (status, started) = await Add("{\"path\":\"/work/demo\"}");
        var job = await Finished(started);

        Assert.Equal(HttpStatusCode.OK, status);   // like every job route
        Assert.Equal("succeeded", job.GetProperty("state").GetString());
        Assert.Equal("AddRepo", job.GetProperty("kind").GetString());
        Assert.Matches("^[0-9a-f]{16}$", job.GetProperty("key").GetString());
        var text = await File.ReadAllTextAsync(ProfilePath, Token);
        Assert.Contains("name: \"demo\"", text, StringComparison.Ordinal);
        Assert.Contains("path: \"/work/demo\"", text, StringComparison.Ordinal);
        Assert.Contains("heartbeat", text, StringComparison.Ordinal);   // the owner's other keys stayed
    }

    [Fact]
    public async Task The_job_lines_name_the_two_steps_and_no_path()
    {
        var (_, started) = await Add("{\"path\":\"/work/demo\"}");
        var job = await Finished(started);

        var lines = job.GetProperty("progress").EnumerateArray().Select(static l => l.GetProperty("line").GetString()).ToArray();

        Assert.Equal(["checking", "added to the profile"], lines);
        Assert.DoesNotContain("/work/demo", job.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_ledger_has_repo_added_with_the_repo_and_the_folder()
    {
        var (_, started) = await Add("{\"path\":\"/work/demo\"}");
        _ = await Finished(started);

        var ledger = await Host.App.Services.GetRequiredService<ILedgerReader>().Scan(0, 100, new LedgerFilter(TypeFilter.Parse(EventTypes.RepoAdded).Value, Item: null), Token);

        var recorded = Assert.Single(ledger.Value.Events);
        Assert.Contains("\"repo\":\"demo\"", recorded.Envelope.Payload, StringComparison.Ordinal);
        Assert.Contains("/work/demo", recorded.Envelope.Payload, StringComparison.Ordinal);   // the non-public payload; the public form (RepoAddedPublic) leaves it out
    }

    [Fact]
    public async Task A_second_add_of_the_same_folder_after_the_first_ended_is_SND_SETUP_002()
    {
        var (_, started) = await Add("{\"path\":\"/work/demo\"}");
        _ = await Finished(started);

        var (status, body) = await Add("{\"path\":\"/work/demo\"}");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("SND-SETUP-002", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_setup_list_shows_the_repo_with_what_git_said()
    {
        var (_, started) = await Add("{\"path\":\"/work/demo\"}");
        _ = await Finished(started);

        var setup = JsonDocument.Parse((await Host.Http.Ask(Get("/sindri/setup", Human(Host.Token.Value)))).Body).RootElement;
        var repo = setup.GetProperty("steps")[0];

        Assert.Equal("repo", repo.GetProperty("id").GetString());
        Assert.Equal("done", repo.GetProperty("state").GetString());
        Assert.Equal("On main at abc1234", repo.GetProperty("items")[0].GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_second_add_of_the_same_folder_while_one_runs_is_SND_JOB_409_naming_the_running_job()
    {
        var running = Core.Ids.JobId.From("JOB-" + new string('b', 32));
        Assert.True(Host.App.Services.GetRequiredService<Features.Jobs.JobRegistry>().TryClaim(Core.Ids.JobKind.From("AddRepo"), RepoFolders.KeyOf("/work/demo"), running, out _));

        var (status, body) = await Add("{\"path\":\"/work/demo\"}");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("SND-JOB-409", body, StringComparison.Ordinal);
        Assert.Contains(running.Value, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_is_refused_and_nothing_is_written()
    {
        var answer = await Host.Http.Ask(Post("/sindri/setup/repos", "{\"path\":\"/work/demo\"}", (Sindri.Host.SindriHeaders.Caller, "session"), (Sindri.Host.SindriHeaders.Client, "cli")));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Contains("SND-AUTH-002", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("demo", await File.ReadAllTextAsync(ProfilePath, Token), StringComparison.Ordinal);
    }
}
