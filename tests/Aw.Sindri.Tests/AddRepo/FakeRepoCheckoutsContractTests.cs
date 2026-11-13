using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

[Lane(TestLane.Unit)]
public sealed class FakeRepoCheckoutsContractTests : RepoCheckoutsContract
{
    private readonly FakeRepoCheckouts _checkouts = new();

    protected override IRepoCheckouts Create() => _checkouts;

    // The contract names every path it asks about; these pin the answer the other tests lean on, for a path nobody named.
    [Fact]
    public void A_path_nobody_named_reads_as_a_checkout() => Assert.Equal(RepoCheckout.Checkout, new FakeRepoCheckouts().Of("/x"));

    [Fact]
    public void A_path_nobody_named_reads_as_the_kind_the_fake_was_given() =>
        Assert.Equal(RepoCheckout.PlainFolder, new FakeRepoCheckouts(RepoCheckout.PlainFolder).Of("/x"));

    protected override string Clone() => Named("/work/clone", RepoCheckout.Checkout);

    protected override string Worktree() => Named("/work/wt-x", RepoCheckout.Checkout);

    protected override string PlainFolder() => Named("/work/notes", RepoCheckout.PlainFolder);

    protected override string PlainFolderInside(string clone) => Named(clone + "/notes", RepoCheckout.PlainFolder);

    protected override string MissingFolder() => Named("/gone/repo", RepoCheckout.Missing);

    private string Named(string path, RepoCheckout kind)
    {
        _ = _checkouts.At(path, kind);
        return path;
    }
}
