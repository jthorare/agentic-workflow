using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class FakeProviderLinearTests
{
    [Fact]
    public async Task The_fake_answers_a_validation_error_to_a_filter_on_history_the_way_Linear_does()
    {
        var provider = new FakeProvider();
        using var http = new HttpClient(provider, disposeHandler: false);
        const string Body = """{"query":"query { issues(first: 50) { nodes { history(first: 25, filter: { createdAt: { gt: $since } }) { nodes { id } } } } }"}""";
        using var content = new StringContent(Body, Encoding.UTF8, "application/json");

        using var response = await http.PostAsync(new Uri("https://api.linear.app/graphql"), content, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Unknown argument", text, StringComparison.Ordinal);
        Assert.Contains("Issue.history", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_filter_on_comments_is_fine()
    {
        var provider = new FakeProvider();
        using var http = new HttpClient(provider, disposeHandler: false);
        const string Body = """{"query":"query { issues(first: 50) { nodes { comments(first: 10, filter: { createdAt: { gt: $since } }) { nodes { id } } } } }"}""";
        using var content = new StringContent(Body, Encoding.UTF8, "application/json");

        using var response = await http.PostAsync(new Uri("https://api.linear.app/graphql"), content, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("errors", text, StringComparison.Ordinal);
    }
}
