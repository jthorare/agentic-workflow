using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnboardRepo;

/// <summary>Onboarding after approval (setup-once spec §5.1): each repo the approved profile names gets the hook slot and 'repo.onboarded' once per approved hash; the live file alone onboards nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RepoOnboardingTests : IDisposable
{
    private readonly SliceHarness _slice = new();
    private readonly CountingHooks _hooks = new();
    private readonly KnownRepoEvents _known = new();
    private IDisposable? _caller;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _caller?.Dispose();

    private RepoOnboarding Onboarding() =>
        new(_slice.Profiles, _hooks, new RepoEvents(_slice.Held, _slice.Store, _slice.Events, _slice.Callers), _known, new RepoGitCache(new FakeRepoInspector(), _slice.Clock));

    private IEnumerable<string> Recorded() => _slice.Store.Events.Where(static e => e.Envelope.Type == EventTypes.RepoOnboarded).Select(static e => e.IdempotencyKey);

    private async Task Approve(char hash, params string[] repos)
    {
        var profile = new SindriProfile(1, "thor", Repos: [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))]);
        _ = await _slice.Profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string(hash, 64)), "/home/profile.yaml"), Token);
    }

    [Fact]
    public async Task Each_repo_of_the_approved_profile_is_onboarded_once_per_hash()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");
        await Approve('a', "demo", "other");
        var onboarding = Onboarding();

        await onboarding.Ensure(Token);
        await onboarding.Ensure(Token);

        Assert.Equal(["repo.onboarded:demo:" + new string('a', 64), "repo.onboarded:other:" + new string('a', 64)], Recorded(), StringComparer.Ordinal);
        Assert.Equal(2, _hooks.Installed);
    }

    [Fact]
    public async Task A_new_approval_onboards_the_repo_again_under_the_new_hash()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");
        var onboarding = Onboarding();
        await Approve('a', "demo");
        await onboarding.Ensure(Token);

        await Approve('b', "demo");
        await onboarding.Ensure(Token);

        Assert.Equal(["repo.onboarded:demo:" + new string('a', 64), "repo.onboarded:demo:" + new string('b', 64)], Recorded(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_profile_that_is_only_live_onboards_nothing()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");

        await Onboarding().Ensure(Token);

        Assert.Empty(Recorded());
        Assert.Equal(0, _hooks.Installed);
    }

    [Fact]
    public async Task Without_the_lease_nothing_runs_and_the_next_try_onboards()
    {
        _caller = _slice.Callers.EnterService("test");
        await Approve('a', "demo");
        var onboarding = Onboarding();

        await onboarding.Ensure(Token);
        Assert.Empty(Recorded());
        Assert.Equal(0, _hooks.Installed);

        _ = await _slice.HoldLease();
        await onboarding.Ensure(Token);

        _ = Assert.Single(Recorded());
    }

    [Fact]
    public async Task A_name_that_is_not_a_repo_name_is_skipped()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");
        await Approve('a', "not a name");

        await Onboarding().Ensure(Token);

        Assert.Empty(Recorded());
    }

    [Fact]
    public async Task The_hook_slot_says_there_is_no_secret_check_yet_so_it_is_never_a_silent_green()
    {
        _ = await _slice.HoldLease();
        _caller = _slice.Callers.EnterService("test");

        var done = await Onboarding().Onboard(ProfileHash.From(new string('a', 64)), new RepoEntry("demo", "/work/demo"), RepoName.From("demo"), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.Equal(("demo", "hooked"), (done.Value.Repo.Value, done.Value.Hooks));
        Assert.Equal("No secret-check hook yet: add it when the installer ships", NoSecretCheckHook.Detail);
    }

    private sealed class CountingHooks : IRepoHooks
    {
        public int Installed { get; private set; }

        public ValueTask<string> Install(RepoEntry repo, CancellationToken cancellationToken)
        {
            Installed++;
            return ValueTask.FromResult("hooked");
        }
    }
}
