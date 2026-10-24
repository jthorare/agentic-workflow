using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Tests.Scrub;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.OnboardRepo;

/// <summary>The real hook slot (cutover spec §6.2 item 2): it installs the v3 hook with the stable ~/.local/bin/sindri baked in, replaces a TS hook, and leaves a foreign one alone.</summary>
[Lane(TestLane.Integration)]
public sealed class PreCommitRepoHooksTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Stable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "sindri");

    [Fact]
    public async Task Install_puts_the_v3_hook_in_the_repo_with_the_stable_path_and_says_so()
    {
        using var repo = await TempGitRepo.Create();
        var hook = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");

        var said = await ScrubRig.Hooks().Install(new RepoEntry("demo", repo.Path), Token);

        var text = await File.ReadAllTextAsync(hook, Token);
        Assert.Contains("# sindri-pre-commit v3", text, StringComparison.Ordinal);
        Assert.Contains($"SINDRI='{Stable}'", text, StringComparison.Ordinal);
        Assert.NotEqual(NoSecretCheckHook.Detail, said, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Install_replaces_a_v2_hook()
    {
        using var repo = await TempGitRepo.Create();
        var hook = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, ScrubFixtures.V2Hook, Token);

        _ = await ScrubRig.Hooks().Install(new RepoEntry("demo", repo.Path), Token);

        Assert.Contains("# sindri-pre-commit v3", await File.ReadAllTextAsync(hook, Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_leaves_a_foreign_hook_alone_and_the_sentence_says_it_did()
    {
        using var repo = await TempGitRepo.Create();
        var hook = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, ScrubFixtures.ForeignHook, Token);

        var said = await ScrubRig.Hooks().Install(new RepoEntry("demo", repo.Path), Token);

        Assert.Equal(ScrubFixtures.ForeignHook, await File.ReadAllTextAsync(hook, Token));
        Assert.Contains("SND-SCRUB-003", said, StringComparison.Ordinal);
    }
}
