using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The real stopper turns every way the POST can fail into status 0, so Stop never throws on the UI thread (#3382).</summary>
[Lane(TestLane.Unit)]
public sealed class HttpSessionStopperTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private sealed class Throwing(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw error;
    }

    [Fact]
    public async Task A_request_that_times_out_answers_0()
    {
        using var http = new HttpClient(new Throwing(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.")));
        var stopper = new HttpSessionStopper(http, new InMemoryHumanToken("tok"));

        Assert.Equal(0, await stopper.Stop(_api, "claude-code", "ideas", TestContext.Current.CancellationToken));
    }
}
