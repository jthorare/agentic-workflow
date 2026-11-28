using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.AddRepo;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The cheap checks an AddRepo start makes inside the gate (setup-once spec §5.1): real error codes before any job exists, and nothing written.</summary>
[Lane(TestLane.Unit)]
public sealed class AddRepoAdmissionTests : IDisposable
{
    private readonly AddRepoRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static CancellationToken Token => AddRepoRig.Token;

    [Fact]
    public async Task A_good_folder_is_admitted_and_its_name_is_the_folder_in_the_repo_name_rules()
    {
        var request = new AddRepoRequest("/work/demo");

        var admitted = await _rig.Admission().Admit(request, Token);

        Assert.True(admitted.IsSuccess);
        Assert.True(_rig.Resolved.Take(request, out var kept));
        Assert.Equal(("demo", "/work/demo"), (kept!.Name.Value, kept.Inspected.Checkout));
    }

    [Fact]
    public async Task The_job_key_is_the_hash_of_the_resolved_path_so_a_worktree_and_its_checkout_share_one_job()
    {
        var first = new AddRepoRequest("/work/demo");
        var second = new AddRepoRequest("/work/wt");
        var admission = _rig.Admission();
        _ = await admission.Admit(first, Token);
        _ = await admission.Admit(second, Token);

        Assert.Equal(_rig.Resolved.KeyOf(first), _rig.Resolved.KeyOf(second));
        Assert.Equal(RepoFolders.KeyOf("/work/demo"), _rig.Resolved.KeyOf(first));
    }

    [Theory]
    [InlineData("")]
    [InlineData("work/demo")]
    [InlineData("~/demo")]
    public async Task A_path_that_is_not_absolute_is_SND_SETUP_001(string path)
    {
        var admitted = await _rig.Admission().Admit(new AddRepoRequest(path), Token);

        Assert.Equal("SND-SETUP-001", admitted.Error.Code);
    }

    [Fact]
    public async Task A_folder_git_does_not_accept_is_SND_SETUP_001_with_its_sentence()
    {
        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/missing"), Token);

        Assert.Equal("SND-SETUP-001", admitted.Error.Code);
        Assert.Contains("fix: pick the repo's top folder", admitted.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_name_that_is_not_a_repo_name_is_SND_SETUP_002()
    {
        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/demo", "my repo"), Token);

        Assert.Equal("SND-SETUP-002", admitted.Error.Code);
        Assert.Contains("'my repo' is not a repo name", admitted.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_given_name_is_kept_as_it_is_when_it_is_valid()
    {
        var request = new AddRepoRequest("/work/demo", "Demo_1.0");

        _ = await _rig.Admission().Admit(request, Token);

        Assert.True(_rig.Resolved.Take(request, out var kept));
        Assert.Equal("Demo_1.0", kept!.Name.Value);
    }

    [Fact]
    public async Task A_repo_already_in_the_profile_is_SND_SETUP_002_and_changes_nothing()
    {
        _rig.ProfileNames(("demo", "/work/demo"));

        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/demo", "second"), Token);

        Assert.Equal("SND-SETUP-002", admitted.Error.Code);
        Assert.Contains("already the repo demo", admitted.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_rig.Writer.Repos);
    }

    [Fact]
    public async Task A_worktree_of_a_repo_in_the_profile_is_that_repo()
    {
        _rig.ProfileNames(("demo", "/work/demo"));

        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/wt"), Token);

        Assert.Equal("SND-SETUP-002", admitted.Error.Code);
    }

    [Fact]
    public async Task A_name_another_repo_has_is_SND_SETUP_002_whatever_its_case()
    {
        _rig.ProfileNames(("Demo", "/work/else"));

        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/demo"), Token);

        Assert.Equal("SND-SETUP-002", admitted.Error.Code);
        Assert.Contains("name demo is taken", admitted.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_profile_file_yet_the_first_repo_is_admitted()
    {
        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/demo"), Token);

        Assert.True(admitted.IsSuccess);
    }

    [Fact]
    public async Task A_rolled_back_start_gives_the_folder_up()
    {
        var request = new AddRepoRequest("/work/demo");
        var admission = _rig.Admission();
        _ = await admission.Admit(request, Token);

        admission.Rollback(request);

        Assert.False(_rig.Resolved.Take(request, out _));
    }

    [Fact]
    public async Task A_duplicate_whose_event_is_missing_gets_repo_added_appended_and_the_start_is_still_refused()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.ProfileNames(("demo", "/work/demo"));

        var admitted = await _rig.Admission().Admit(new AddRepoRequest("/work/demo"), Token);

        Assert.Equal("SND-SETUP-002", admitted.Error.Code);
        var added = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
        Assert.Equal("repo.added:demo", added.IdempotencyKey);
    }

    [Fact]
    public async Task A_duplicate_that_has_its_event_gets_no_second_one()
    {
        await _rig.Lease();
        var service = _rig.Service();
        var request = new AddRepoRequest("/work/demo");
        _ = await _rig.Admission().Admit(request, Token);
        _ = await service.Handle(request, Token);
        _rig.ProfileNames(("demo", "/work/demo"));

        _ = await _rig.Admission().Admit(new AddRepoRequest("/work/demo"), Token);

        _ = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
    }
}
