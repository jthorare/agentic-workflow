using Aw.Kernel.Testing;
using Aw.Sindri.Features.ResolveLinks;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1g (messages spec §9.3, #3021): a link in message text resolves to its row key, or 'none'.</summary>
[Lane(TestLane.Unit)]
public sealed class ResolveLinksTests
{
    [Fact]
    public Task Resolve_github_pr_url_to_row_key() => RedUntil.Phase("M1g", async () =>
    {
        var url = new Uri("https://github.com/acme/harbor/pull/3120");

        var answer = await new ResolveLinksService().Handle(new ResolveLinksRequest([url]), TestContext.Current.CancellationToken);

        Assert.True(answer.IsSuccess);
        var link = Assert.Single(answer.Value.Links);
        Assert.Equal((url, "github:pr:acme/harbor#3120"), (link.Url, link.RowKey));
    });
}
