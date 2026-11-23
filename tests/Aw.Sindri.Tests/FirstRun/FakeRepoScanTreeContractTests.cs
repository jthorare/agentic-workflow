using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FirstRun;

[Lane(TestLane.Unit)]
public sealed class FakeRepoScanTreeContractTests : RepoScanTreeContract
{
    private const string Home = "/home/joi";

    private readonly FakeRepoScanTree _tree = new(Home);

    protected override IRepoScanTree Create() => _tree;

    protected override string HomeFolder() => Home;

    protected override string Folder(string relative) => Named(relative, static (tree, path) => tree.Folder(path));

    protected override string Checkout(string relative, string? origin) => Named(relative, (tree, path) => tree.Checkout(path, origin));

    protected override string Worktree(string relative, string main) => Named(relative, (tree, path) => tree.Worktree(path, main));

    protected override string Link(string relative, string target) => Named(relative, static (tree, path) => tree.Link(path));

    private string Named(string relative, Func<FakeRepoScanTree, string, FakeRepoScanTree> add)
    {
        var path = Home + "/" + relative;
        _ = add(_tree, path);
        return path;
    }
}
