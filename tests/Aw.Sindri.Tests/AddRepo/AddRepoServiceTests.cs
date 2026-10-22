using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The work of an AddRepo job (setup-once spec §5.1): the profile write, the two progress lines, and 'repo.added' with the repo's name.</summary>
[Lane(TestLane.Unit)]
public sealed class AddRepoServiceTests : IDisposable
{
    private readonly AddRepoRig _rig = new();
    private readonly Lines _lines = new();
    private readonly IDisposable _job;

    private static CancellationToken Token => AddRepoRig.Token;

    // The caller and the job are entered here, in the constructor: an async helper's AsyncLocal would be gone when it returns to the test.
    public AddRepoServiceTests()
    {
        _rig.Enter();
        _job = _rig.Jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), JobKind.From("AddRepo"), _lines));
    }

    public void Dispose()
    {
        _job.Dispose();
        _rig.Dispose();
    }

    private async Task<(AddRepoService Service, AddRepoRequest Request)> Admitted(string path = "/work/demo", string? name = null)
    {
        await _rig.Lease();
        var service = _rig.Service();
        var request = new AddRepoRequest(path, name);
        Assert.True((await _rig.Admission().Admit(request, Token)).IsSuccess);
        return (service, request);
    }

    [Fact]
    public async Task The_job_writes_the_repo_to_the_profile_says_so_and_records_repo_added()
    {
        var (service, request) = await Admitted();

        var added = await service.Handle(request, Token);

        Assert.Equal(("demo", true), (added.Value.Repo.Value, added.Value.Created));
        Assert.Equal([("demo", "/work/demo")], _rig.Writer.Repos.Select(static r => (r.Name, r.Path)));
        Assert.Equal(["checking", "added to the profile"], _lines.Seen);
        var recorded = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
        Assert.Equal("repo.added:demo", recorded.IdempotencyKey);
        var payload = _rig.Slice.Events.Payload<RepoAdded>(recorded).Value;
        Assert.Equal(("demo", "/work/demo"), (payload.Repo.Value, payload.Path));
    }

    [Fact]
    public async Task The_first_repo_creates_the_profile_with_the_effects_the_shipped_recipes_declare()
    {
        var (service, request) = await Admitted();

        _ = await service.Handle(request, Token);

        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], _rig.Writer.CreatedWith);
    }

    [Fact]
    public async Task A_second_repo_is_appended_and_not_created()
    {
        var (firstService, firstRequest) = await Admitted();
        _ = await firstService.Handle(firstRequest, Token);
        var (secondService, secondRequest) = await Admitted("/work/other");

        var added = await secondService.Handle(secondRequest, Token);

        Assert.False(added.Value.Created);
        Assert.Equal(2, _rig.Writer.Repos.Count);
    }

    [Fact]
    public async Task The_progress_lines_never_carry_the_path()
    {
        var (service, request) = await Admitted();
        _ = await service.Handle(request, Token);

        Assert.DoesNotContain("/work/demo", string.Join('\n', _lines.Seen), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_that_stopped_being_a_checkout_since_the_start_fails_the_job_and_writes_nothing()
    {
        var (service, request) = await Admitted();
        var gone = new FakeRepoInspector();
        var late = new AddRepoService(_rig.Resolved, gone, _rig.Writer, _rig.Shipped, _rig.Recorder, _rig.Jobs, _rig.Git);
        _ = service;

        var added = await late.Handle(request, Token);

        Assert.Equal("SND-SETUP-001", added.Error.Code);
        Assert.Empty(_rig.Writer.Repos);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
    }

    [Fact]
    public async Task A_profile_write_that_fails_is_the_jobs_failure_code_and_no_event_is_recorded()
    {
        var (service, request) = await Admitted();
        _rig.Writer.Fails = Core.SindriErrors.ProfileInvalid.With("the profile changed while Sindri was writing it");

        var added = await service.Handle(request, Token);

        Assert.Equal("SND-PROFILE-002", added.Error.Code);
        Assert.Equal(["checking"], _lines.Seen);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
    }

    [Fact]
    public async Task A_repo_a_racing_job_already_added_is_SND_SETUP_002_and_records_nothing()
    {
        var (service, request) = await Admitted();
        _ = await _rig.Writer.Append(new Core.Profile.RepoEntry("demo", "/work/demo"), [], Token);

        var added = await service.Handle(request, Token);

        Assert.Equal("SND-SETUP-002", added.Error.Code);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
    }

    [Fact]
    public async Task A_job_with_no_resolved_folder_fails_rather_than_guessing()
    {
        await _rig.Lease();
        var service = _rig.Service();

        var added = await service.Handle(new AddRepoRequest("/work/demo"), Token);

        Assert.True(added.IsError);
        Assert.Empty(_rig.Writer.Repos);
    }

    [Fact]
    public async Task Without_the_lease_the_job_writes_nothing()
    {
        var (_, request) = await Admitted();
        var noLease = new AddRepoService(_rig.Resolved, _rig.Inspector, _rig.Writer, _rig.Shipped, new RepoEvents(new Core.Lease.HeldLease(), _rig.Slice.Store, _rig.Slice.Events, _rig.Slice.Callers), _rig.Jobs, _rig.Git);

        var added = await noLease.Handle(request, Token);

        Assert.Equal("SND-API-001", added.Error.Code);
        Assert.Empty(_rig.Writer.Repos);
    }

    [Fact]
    public async Task Adding_a_repo_makes_the_git_facts_be_asked_again()
    {
        var (service, request) = await Admitted();
        _ = await _rig.Git.Of("/work/demo", Token);
        _ = await _rig.Git.Of("/work/demo", Token);
        Assert.Equal(1, _rig.Inspector.Asked);

        _ = await service.Handle(request, Token);
        _ = await _rig.Git.Of("/work/demo", Token);

        Assert.Equal(2, _rig.Inspector.Asked);
    }

    private sealed class Lines : IProgress<string>
    {
        private readonly List<string> _seen = [];

        public IReadOnlyList<string> Seen => _seen;

        public void Report(string value) => _seen.Add(value);
    }
}
