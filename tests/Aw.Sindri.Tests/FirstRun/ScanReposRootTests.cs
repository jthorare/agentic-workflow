using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ScanRepos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>Who may scan and where (first-run spec §3.3, gap 7): the root stays in the home folder, a launched session is refused, and each row says whether GitHub or the profile knows it.</summary>
public sealed class ScanReposRootTests
{
    private const string Home = "/home/joi";

    private readonly SliceHarness _slice = new();
    private readonly FakeRepoScanTree _tree = new(Home);
    private readonly FakeKnownRepos _known = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData("/etc")]
    [InlineData("/home/joiner")]
    [InlineData("/home/joi/gone")]
    [InlineData("relative/path")]
    public async Task A_root_outside_the_home_folder_or_missing_is_SND_SETUP_008(string root)
    {
        _ = _tree.Folder("/etc").Folder("/home/joiner");

        var result = await Service().Handle(new ScanReposRequest(root, Depth: null), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SetupScanRootInvalid.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("~/repos")]
    [InlineData("/home/joi/repos")]
    public async Task A_root_under_the_home_folder_walks_only_that_folder(string root)
    {
        _ = _tree.Checkout(Home + "/repos/app").Checkout(Home + "/other/lib");

        var rows = await Rows(new ScanReposRequest(root, Depth: null));

        Assert.Equal([Home + "/repos/app"], rows.Select(static r => r.Path), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(7)]
    public async Task A_depth_outside_1_to_6_is_invalid_input(int depth)
    {
        var result = await Service().Handle(new ScanReposRequest(Root: null, depth), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_session_is_refused_with_SND_AUTH_002()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await Service().Handle(new ScanReposRequest(Root: null, Depth: null), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_owner_started_session_may_scan()
    {
        _ = _tree.Checkout(Home + "/repos/app");
        using var entered = _slice.Callers.Enter(A.Caller().Session().Build());

        var rows = await Rows(new ScanReposRequest(Root: null, Depth: null));

        _ = Assert.Single(rows);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_the_account_worked_on_lately_is_known()
    {
        _ = _tree.Checkout(Home + "/repos/app", "git@github.com:Acme/App.git").Checkout(Home + "/repos/lib", "https://github.com/acme/lib");
        _ = _known.With("acme/app");

        var rows = await Rows(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal([(Home + "/repos/app", true), (Home + "/repos/lib", false)], rows.Select(static r => (r.Path, r.Known)));
        Assert.Equal(1, _known.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_GitHub_read_ticks_nothing_and_says_so_on_the_last_line()
    {
        _ = _tree.Checkout(Home + "/repos/app", "https://github.com/acme/app.git");
        _known.Answer = SindriErrors.ApiInternal.With("GitHub answered 502");
        var response = await Service().Handle(new ScanReposRequest(Root: null, Depth: null), Token);

        var rows = new List<RepoScanRow>();
        await foreach (var row in response.Value.Rows.WithCancellation(Token))
        {
            rows.Add(row);
        }

        Assert.False(Assert.Single(rows).Known);
        Assert.True(response.Value.End().KnownFailed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_folder_the_profile_names_is_in_the_profile()
    {
        _ = _tree.Checkout(Home + "/repos/app").Checkout(Home + "/repos/lib");
        var profile = new SindriProfile(1, "o", Repos: [new RepoEntry("app", Home + "/repos/app/")]);
        _slice.Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "profile.yaml");

        var rows = await Rows(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal([(Home + "/repos/app", true), (Home + "/repos/lib", false)], rows.Select(static r => (r.Path, r.InProfile)));
    }

    private ScanReposService Service() => ScanReposServiceFactory.Create(_slice, _tree, _known);

    private async Task<List<RepoScanRow>> Rows(ScanReposRequest request)
    {
        var response = await Service().Handle(request, Token);
        Assert.True(response.IsSuccess, response.IsError ? response.Error.ToString() : null);
        var rows = new List<RepoScanRow>();
        await foreach (var row in response.Value.Rows.WithCancellation(Token))
        {
            rows.Add(row);
        }

        return rows;
    }
}
