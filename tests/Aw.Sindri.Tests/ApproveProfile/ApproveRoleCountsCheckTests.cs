using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Worktrees;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;
using Aw.Sindri.Tests.Workflow.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>
/// The bug check for issue 1148: 'profile approve' lists a changed 'index.roles.&lt;role&gt;' or 'index.demote' key but not how many files on the repo's HEAD the role matches, so the owner can't see
/// what a role change does before approving it. The preview should say 'repos.web.index.roles.test: 3 files on HEAD (was 0)', or 'count unavailable' when the files can't be listed.
/// </summary>
public sealed class ApproveRoleCountsCheckTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The service as the container builds it: the harness's fakes and the real git file lister, whatever the service's constructor takes.</summary>
    private ApproveProfileService Service()
    {
        var services = new ServiceCollection()
            .AddSingleton<IProfileSource>(_h.Profiles)
            .AddSingleton<IApprovals>(_h.Profiles)
            .AddSingleton(_h.Held)
            .AddSingleton<ILedgerStore>(_h.Store)
            .AddSingleton(_h.Events)
            .AddSingleton(new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()))
            .AddSingleton<ITrackedFiles, GitTrackedFiles>();
        using var provider = services.BuildServiceProvider();
        return ActivatorUtilities.CreateInstance<ApproveProfileService>(provider);
    }

    private static LoadedProfile Loaded(string path, IndexSettings index)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("web", path, index)])).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(ProfileReading.Canonical(profile, ProfileFiles.Options)), "/home/profile.yaml");
    }

    private static IndexSettings Roles(params string[] globs) =>
        new(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["test"] = globs });

    private static async Task<TempGitRepo> RepoWithThreeTests()
    {
        var repo = await TempGitRepo.Create();
        foreach (var file in new[] { "src/a.test.ts", "src/b.test.ts", "src/c.test.ts", "src/main.ts" })
        {
            _ = Directory.CreateDirectory(Path.Combine(repo.Path, "src"));
            await File.WriteAllTextAsync(Path.Combine(repo.Path, file), "x", Token);
        }

        _ = await repo.Git("add", "-A");
        return repo;
    }

    /// <summary>The preview as the CLI gets it: the response in JSON, which carries every line the CLI prints.</summary>
    private async Task<string> Preview(LoadedProfile live)
    {
        _h.Profiles.Live = live;
        var shown = await Service().Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token);
        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        return JsonSerializer.Serialize(shown.Value, ProfileFiles.Options);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_role_shows_how_many_files_on_head_it_matches()
    {
        using var repo = await RepoWithThreeTests();

        var shown = await Preview(Loaded(repo.Path, Roles("**/*.test.ts")));

        Assert.Contains("repos.web.index.roles.test: 3 files on HEAD (was 0)", shown, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_changed_role_shows_the_count_under_the_old_globs_as_was()
    {
        using var repo = await RepoWithThreeTests();
        _ = await _h.Profiles.Save(Loaded(repo.Path, Roles("src/main.ts")), Token);

        var shown = await Preview(Loaded(repo.Path, Roles("**/*.test.ts")));

        Assert.Contains("repos.web.index.roles.test: 3 files on HEAD (was 1)", shown, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_changed_demote_list_shows_the_files_of_the_role_it_names()
    {
        using var repo = await RepoWithThreeTests();
        var approved = Roles("**/*.test.ts");
        _ = await _h.Profiles.Save(Loaded(repo.Path, approved), Token);

        var shown = await Preview(Loaded(repo.Path, approved with { Demote = ["test"] }));

        Assert.Contains("repos.web.index.demote.test: 3 files on HEAD (was 0)", shown, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_whose_files_cannot_be_listed_says_count_unavailable()
    {
        var shown = await Preview(Loaded(Path.Combine(Path.GetTempPath(), "aw-1148-no-such-checkout"), Roles("**/*.test.ts")));

        Assert.Contains("repos.web.index.roles.test: count unavailable", shown, StringComparison.Ordinal);
    }
}
