using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// Doctor's first-map row names only the repos the map waits on (#3216): a checkout off its default branch is left out of the map and gets no code index build,
/// so naming it would send the person to wait for an index that never comes.
/// </summary>
public sealed class FirstMapWaitedOnReposTests
{
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(params string[] repos)
    {
        var loaded = SliceHarness.Profile("5s");
        return loaded with { Profile = loaded.Profile with { Repos = [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))] } };
    }

    private static RepoHead Head(string repo, bool onDefault) => new(ContextRepoName.From(repo), new string('1', 40), onDefault);

    private async Task<DoctorCheck> Row(LoadedProfile profile) =>
        await new FirstMapDoctorCheck(new InMemoryJobViews(), new NoPauses(), _engine.Proxy).Check(profile, Ct);

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_off_its_default_branch_is_not_named_as_one_the_map_waits_on()
    {
        _engine.Heads = [Head("demo", onDefault: true), Head("side", onDefault: false)];

        var row = await Row(Profile("demo", "side"));

        Assert.Equal((CheckState.Ok, "no map yet: the first map builds after the code index of demo"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_repo_off_its_default_branch_warns_and_names_them()
    {
        _engine.Heads = [Head("side", onDefault: false)];

        var row = await Row(Profile("side"));

        Assert.Equal((CheckState.Warn, "no map yet: no mapped repo is on its default branch (side)"), (row.State, row.Detail));
        Assert.NotNull(row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_no_head_stays_named_and_a_failed_heads_read_names_every_repo()
    {
        var missing = await Row(Profile("demo"));
        _engine.Failure = new Error("SND-CONTEXT-048", ErrorKind.Invalid, "not a checkout");
        var failed = await Row(Profile("demo", "side"));

        Assert.Equal("no map yet: the first map builds after the code index of demo", missing.Detail);
        Assert.Equal("no map yet: the first map builds after the code index of demo, side", failed.Detail);
    }
}
