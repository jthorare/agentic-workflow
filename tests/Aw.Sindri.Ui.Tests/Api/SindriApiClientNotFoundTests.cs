using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's own reading of a bare status and the helper that tells a 404 from the rest agree on the error's text.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientNotFoundTests
{
    private const string Item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task A_bare_404_on_the_item_read_is_not_found()
    {
        var read = await Client(new Canned(HttpStatusCode.NotFound, string.Empty)).Item(_api, Item, Token);

        Assert.True(read.IsError);
        Assert.True(read.Error.IsNotFound());
    }

    [Fact]
    public async Task A_bare_500_on_the_item_read_is_not_a_not_found()
    {
        var read = await Client(new Canned(HttpStatusCode.InternalServerError, string.Empty)).Item(_api, Item, Token);

        Assert.True(read.IsError);
        Assert.False(read.Error.IsNotFound());
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
    }
}
