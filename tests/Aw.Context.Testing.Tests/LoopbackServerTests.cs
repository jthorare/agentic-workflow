using System.Net;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The fakes' shared host: an exception a handler didn't map becomes a recorded JSON 500 that names it.</summary>
public sealed class LoopbackServerTests
{
    [Fact]
    [Lane(TestLane.Integration)]
    public async Task An_unanticipated_exception_is_a_recorded_json_500_naming_it()
    {
        await using var server = await LoopbackServer.Start("throwing", _ => throw new FormatException("planted failure"));

        var (status, body) = await Http.Post(server.BaseUrl, authorization: null, "{}");

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Contains("FormatException", (string?)body!["error"], StringComparison.Ordinal);
        Assert.Contains("planted failure", (string?)body!["error"], StringComparison.Ordinal);
        Assert.Equal(500, Assert.Single(server.Requests).Status);
        Assert.Equal("127.0.0.1", server.BaseUrl.Host);
    }

    [Fact]
    [Lane(TestLane.Unit)]
    public void A_request_never_prints_its_authorization()
    {
        var request = new FakeHttpRequest("POST", "/graphql", new Dictionary<string, string>(StringComparer.Ordinal), "Bearer planted-91c2", "{}");

        Assert.DoesNotContain("planted-91c2", request.ToString(), StringComparison.Ordinal);
    }
}
