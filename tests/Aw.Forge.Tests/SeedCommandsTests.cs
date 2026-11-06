using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Host;
using Aw.Forge.Host.Commands;
using Aw.Kernel.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Forge.Tests;

/// <summary>The 'seed files' and 'seed checks --runs' commands over the file forge in a temporary state directory.</summary>
public sealed class SeedCommandsTests : IDisposable
{
    private readonly DirectoryInfo _state = Directory.CreateTempSubdirectory("aw-forge-seed-");

    public void Dispose() => _state.Delete(recursive: true);

    [Fact, Trait("Lane", "unit")]
    public async Task Seed_files_puts_the_files_and_their_texts_where_Files_and_FileText_read_them()
    {
        await using var provider = Provider();
        var forge = provider.GetRequiredService<IForge>();
        var pull = await OpenPull(forge);
        var from = Path.Combine(_state.FullName, "files.json");
        await File.WriteAllTextAsync(from, string.Join(
            '\n',
            "[",
            """{"file":{"path":"src/a.cs","previousPath":null,"status":"Modified","additions":2,"deletions":1,"patch":"@@ -1 +1,2 @@"},"text":"class A;"},""",
            """{"file":{"path":"src/b.cs","previousPath":"src/old-b.cs","status":"Renamed","additions":0,"deletions":0,"patch":null},"text":"class B;"},""",
            """{"file":{"path":"logo.png","previousPath":null,"status":"Added","additions":0,"deletions":0,"patch":null},"text":null}""",
            "]"), TestContext.Current.CancellationToken);

        var exit = await Run(provider, "seed", "files", pull.Link, "--from", from);
        var files = (await forge.Files(pull, TestContext.Current.CancellationToken)).Value;
        var snapshot = (await forge.GetPull(pull, TestContext.Current.CancellationToken)).Value;
        var text = await forge.FileText(pull, "src/a.cs", snapshot.Head.Sha, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal(["src/a.cs", "src/b.cs", "logo.png"], files.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal("src/old-b.cs", files.Files[1].PreviousPath);
        Assert.Equal(PullFileStatus.Renamed, files.Files[1].Status);
        Assert.Equal("class A;", text.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Seed_checks_with_runs_puts_the_runs_on_the_pull_request()
    {
        await using var provider = Provider();
        var forge = provider.GetRequiredService<IForge>();
        var pull = await OpenPull(forge);

        var exit = await Run(provider, "seed", "checks", pull.Link, "--state", "failing", "--failing", "lint", "--runs", "build:success:github-actions,lint:failure");
        var checks = (await forge.GetPull(pull, TestContext.Current.CancellationToken)).Value.Checks;

        Assert.Equal(0, exit);
        Assert.Equal(
            [new CheckRun("build", "github-actions", "success", new string('a', 40)), new CheckRun("lint", App: null, "failure", new string('a', 40))],
            checks.Runs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Seed_files_without_from_is_a_usage_error()
    {
        await using var provider = Provider();

        var exit = await Run(provider, "seed", "files", "https://forge.test/acme/widgets/pull/1");

        Assert.Equal(2, exit);
    }

    private ServiceProvider Provider()
    {
        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => _state.FullName, "AW_FORGE_ADAPTER" => "file", _ => null },
            new FakeTimeProvider());
        return services.BuildServiceProvider();
    }

    private static async Task<Core.Refs.ForgeRef> OpenPull(IForge forge) =>
        (await forge.OpenPull(new OpenPullRequest(new Core.Refs.ForgeRepo("forge.test", "acme", "widgets"), "feature/seed", "main", new string('a', 40), "T", "B", Draft: false), TestContext.Current.CancellationToken)).Value;

    private static Task<int> Run(ServiceProvider provider, params string[] args) =>
        CommandTable.Run(args, provider, AwJson.CreateOptions(ForgeJsonContext.Default));
}
