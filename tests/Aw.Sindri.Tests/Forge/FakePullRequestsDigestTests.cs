using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class FakePullRequestsDigestTests
{
    [Fact]
    public async Task Digest_over_21_links_makes_two_batches_and_keeps_the_order()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        var repo = new PullRepo("forge.test", "acme", "widgets");
        var links = Enumerable.Range(1, 21).Select(i => pulls.Add(repo, "feature/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), new string('a', 40)).Link).ToList();

        var result = await pulls.Digest(links, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(links, result.Value.Select(static d => d.Link), StringComparer.Ordinal);
        Assert.Equal([20, 1], pulls.DigestSizes);
    }
}
