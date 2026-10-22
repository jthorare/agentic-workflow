using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnboardRepo;

/// <summary>The OnboardRepo job (setup-once spec §5.1): the start checks the approved snapshot (SND-SETUP-007, 004) before any job exists, the work records 'repo.onboarded' for the approved hash, and nothing here approves a profile.</summary>
[Lane(TestLane.Unit)]
public sealed class OnboardRepoJobTests : IDisposable
{
    private static readonly ProfileHash _hash = ProfileHash.From(new string('a', 64));
    private readonly SliceHarness _slice = new();
    private readonly ResolvedOnboardRepos _resolved = new();
    private readonly FakeRepoLocator _locator = new FakeRepoLocator().In("/work/demo", "/work/demo").In("/work/demo/src", "/work/demo").In("/work/elsewhere", "/work/elsewhere");
    private IDisposable? _caller;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _caller?.Dispose();

    private OnboardRepoAdmission Admission() => new(_slice.Profiles, _locator, _resolved);

    private OnboardRepoService Service() =>
        new(_resolved, _slice.Profiles, new RepoOnboarding(_slice.Profiles, new NoSecretCheckHook(), new RepoEvents(_slice.Held, _slice.Store, _slice.Events, _slice.Callers), new KnownRepoEvents(), new RepoGitCache(new FakeRepoInspector(), _slice.Clock)), new FakeJobContextAccessor());

    private async Task Approve(params string[] repos)
    {
        var profile = new SindriProfile(1, "thor", Repos: [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))]);
        _ = await _slice.Profiles.Save(new LoadedProfile(profile, _hash, "/home/profile.yaml"), Token);
    }

    [Fact]
    public async Task A_folder_inside_a_repo_of_the_approved_profile_is_admitted_with_the_hashed_key()
    {
        await Approve("demo");
        var request = new OnboardRepoRequest("/work/demo/src");

        var admitted = await Admission().Admit(request, Token);

        Assert.True(admitted.IsSuccess);
        Assert.Equal("onboard-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("demo")))[..16], _resolved.KeyOf(request).Value);
    }

    [Fact]
    public async Task A_folder_the_approved_profile_does_not_name_is_SND_SETUP_004()
    {
        await Approve("demo");

        var admitted = await Admission().Admit(new OnboardRepoRequest("/work/elsewhere"), Token);

        Assert.Equal("SND-SETUP-004", admitted.Error.Code);
    }

    [Fact]
    public async Task No_approved_profile_is_SND_SETUP_007_even_when_the_live_file_names_the_repo()
    {
        _slice.Profiles.Live = new LoadedProfile(new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo")]), _hash, "/home/profile.yaml");

        var admitted = await Admission().Admit(new OnboardRepoRequest("/work/demo"), Token);

        Assert.Equal("SND-SETUP-007", admitted.Error.Code);
    }

    [Fact]
    public async Task The_job_records_repo_onboarded_for_the_approved_hash_and_approves_nothing()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");
        await Approve("demo");
        var request = new OnboardRepoRequest("/work/demo");
        _ = await Admission().Admit(request, Token);

        var done = await Service().Handle(request, Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.Equal(("demo", NoSecretCheckHook.Detail), (done.Value.Repo.Value, done.Value.Hooks));
        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoOnboarded);
        Assert.Equal("repo.onboarded:demo:" + _hash.Value, recorded.IdempotencyKey);
        Assert.DoesNotContain(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.ProfileApproved);
    }

    [Fact]
    public async Task Onboarding_the_same_approval_again_leaves_one_event()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");
        await Approve("demo");
        for (var i = 0; i < 2; i++)
        {
            var request = new OnboardRepoRequest("/work/demo");
            _ = await Admission().Admit(request, Token);
            Assert.True((await Service().Handle(request, Token)).IsSuccess);
        }

        _ = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoOnboarded);
    }

    [Fact]
    public async Task A_job_that_was_never_admitted_is_an_internal_error()
    {
        var done = await Service().Handle(new OnboardRepoRequest("/work/demo"), Token);

        Assert.Equal("SND-API-005", done.Error.Code);
    }

    [Fact]
    public async Task A_repo_that_left_the_approved_profile_since_the_start_is_SND_SETUP_004()
    {
        await Approve("demo");
        var request = new OnboardRepoRequest("/work/demo");
        _ = await Admission().Admit(request, Token);
        await Approve("other");

        var done = await Service().Handle(request, Token);

        Assert.Equal("SND-SETUP-004", done.Error.Code);
    }
}
