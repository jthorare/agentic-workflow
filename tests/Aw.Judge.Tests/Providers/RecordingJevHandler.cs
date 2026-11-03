using System.Net;
using System.Text;

namespace Aw.Judge.Tests.Providers;

/// <summary>An HttpMessageHandler standing in for Jev: records each request (multipart parts included) and answers from a function.</summary>
/// <param name="answer">The response body for a request.</param>
/// <param name="status">The status to answer with.</param>
internal sealed class RecordingJevHandler(Func<RecordedJevRequest, string> answer, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly List<RecordedJevRequest> _requests = [];

    public IReadOnlyList<RecordedJevRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Jev always picks <paramref name="choice"/>, with confidence 0.9 and no reasons.</summary>
    public static RecordingJevHandler Choosing(string choice) => new(_ => JevImages.Choice(choice));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var recorded = await RecordedJevRequest.Capture(request, cancellationToken);
        lock (_requests)
        {
            _requests.Add(recorded);
        }

        return new HttpResponseMessage(status) { Content = new StringContent(answer(recorded), Encoding.UTF8, "application/json") };
    }
}
