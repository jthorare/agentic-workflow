using System.Globalization;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class GitReposContractTests : ReposContract, IDisposable
{
    private readonly TempDirectory _temp = new();
    private int _next;

    protected override IGit Git { get; } = new GitProcess();

    protected override IFiles Files { get; } = new LocalFiles();

    protected override string Outside => _temp.Path;

    public void Dispose() => _temp.Dispose();

    protected override (string Root, string Commit) NewRepo()
    {
        var root = _temp.Repo(string.Create(CultureInfo.InvariantCulture, $"r{++_next}"), InMemoryReposContractTests.Base);
        _ = Directory.CreateDirectory(Path.Combine(root, "src"));
        return (root, TempDirectory.Git(root, "rev-parse", "HEAD"));
    }

    protected override void Write(string root, string path, string text) => TempDirectory.Write(root, path, text);

    protected override void Delete(string root, string path) => File.Delete(Path.Combine(root, path));

    protected override void Link(string root, string link, string target) =>
        _ = File.CreateSymbolicLink(Path.Combine(root, link), target);

    protected override string Commit(string root)
    {
        _ = TempDirectory.Git(root, "add", "-A");
        _ = TempDirectory.Git(root, "commit", "-q", "-m", "next");
        return TempDirectory.Git(root, "rev-parse", "HEAD");
    }

    /// <summary>The branch change is what the baseline holds past main; a baseline on main holds nothing.</summary>
    [Fact]
    public void Branch_change_is_what_the_baseline_holds_past_main()
    {
        var (root, onMain) = NewRepo();
        _ = TempDirectory.Git(root, "checkout", "-q", "-b", "red");
        Write(root, "src/wiring.txt", "wired\n");
        var red = Commit(root);

        var change = Git.BranchChange(root, red);
        Assert.True(change.IsSuccess);
        Assert.Contains("src/wiring.txt", change.Value.Stat, StringComparison.Ordinal);
        var none = Git.BranchChange(root, onMain);
        Assert.True(none.IsSuccess);
        Assert.True(string.IsNullOrEmpty(none.Value.Stat));
    }
}
