using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>
/// The contract every <see cref="IPrepareSource"/> honours. The scenario is one repo with two commits: the first has <c lang="text">scripts/package-lock.json</c> as "LOCK-A" (no trailing newline),
/// the second has it as "LOCK-B", and no commit has <c lang="text">other/package-lock.json</c>.
/// </summary>
public abstract class PrepareSourceContract
{
    /// <summary>The path both commits have.</summary>
    protected const string Lockfile = "scripts/package-lock.json";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port and the scenario.</summary>
    /// <returns>The port, the repo, the first commit and the second commit.</returns>
    protected abstract ValueTask<(IPrepareSource Port, string Repo, string First, string Second)> Scenario();

    /// <summary>A file reads back as the commit has it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_lockfile_at_the_base_reads_back_its_text()
    {
        var (port, repo, first, _) = await Scenario();

        var read = await port.FileAtCommit(repo, first, Lockfile, Token);

        Expect.Equal("LOCK-A", read.Value);
    }

    /// <summary>Another commit gives its own text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_different_base_gives_the_other_content()
    {
        var (port, repo, _, second) = await Scenario();

        var read = await port.FileAtCommit(repo, second, Lockfile, Token);

        Expect.Equal("LOCK-B", read.Value);
    }

    /// <summary>A path the commit does not have is no text, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_lockfile_is_no_text()
    {
        var (port, repo, first, _) = await Scenario();

        var read = await port.FileAtCommit(repo, first, "other/package-lock.json", Token);

        Expect.True(read.IsSuccess && read.Value is null);
    }
}
