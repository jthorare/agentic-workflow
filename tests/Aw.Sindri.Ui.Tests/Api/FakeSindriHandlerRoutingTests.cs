using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>How the fake serves a route: the P4 read routes answer reads only.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeSindriHandlerRoutingTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // review run 1, arch-11: the P4 read routes answer GETs only.
    [Theory]
    [InlineData("/sindri/decisions")]
    [InlineData("/sindri/needs-you")]
    public async Task A_post_to_a_P4_read_route_is_not_answered_as_a_read(string route)
    {
        using var http = new HttpClient(new FakeSindriHandler(new FakeSindriApi()));

        using var response = await http.PostAsync(new Uri(_api, route), new StringContent("{}"), TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
    }

}
