using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>AddRepo over in-memory ports: the slice harness (ledger, lease, caller, events), a fake git, a fake profile writer and the git facts cache on the harness clock.</summary>
internal sealed class AddRepoRig : IDisposable
{
    private IDisposable? _caller;
    private bool _leased;

    internal AddRepoRig()
    {
        Git = new RepoGitCache(Inspector, Slice.Clock);
        Recorder = new RepoEvents(Slice.Held, Slice.Store, Slice.Events, Slice.Callers);
        Backfill = new RepoAddedBackfill(Recorder, Slice.Store, Slice.Events, Git, new KnownRepoEvents());
    }

    internal SliceHarness Slice { get; } = new();

    internal FakeRepoInspector Inspector { get; } = new FakeRepoInspector().Has("/work/demo", git: new RepoGit("main", "abc1234")).Has("/work/other").Has("/work/wt", checkout: "/work/demo");

    internal FakeProfileRepoWriter Writer { get; } = new();

    internal FakeShippedEffects Shipped { get; } = new("effect.claim.ref", "effect.notify.desk");

    internal ResolvedRepos Resolved { get; } = new();

    internal RepoGitCache Git { get; }

    internal RepoEvents Recorder { get; }

    internal RepoAddedBackfill Backfill { get; }

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal AddRepoAdmission Admission()
    {
        Enter();
        return new AddRepoAdmission(Inspector, Slice.Profiles, Resolved, Backfill);
    }

    internal AddRepoService Service()
    {
        Enter();
        return new AddRepoService(Resolved, Inspector, Writer, Shipped, Recorder, Jobs, Git);
    }

    /// <summary>Holds the lease, once.</summary>
    internal async Task Lease()
    {
        if (!_leased)
        {
            _ = await Slice.HoldLease();
            _leased = true;
        }
    }

    internal FakeJobContextAccessor Jobs { get; } = new();

    /// <summary>The profile file already names these repos (the admission reads it).</summary>
    internal void ProfileNames(params (string Name, string Path)[] repos)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [.. repos.Select(static r => new RepoEntry(r.Name, r.Path))])).Value;
        Slice.Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(Kernel.Json.AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    internal void Enter() => _caller ??= Slice.Callers.EnterService("test");

    public void Dispose() => _caller?.Dispose();
}
