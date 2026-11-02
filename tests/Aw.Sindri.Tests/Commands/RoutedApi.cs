using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Commands;

/// <summary>An API that answers each route with its canned body and records every call.</summary>
internal sealed class RoutedApi : HttpMessageHandler
{
    internal static readonly GetStatusResponse StatusAtHead7 = new(Running: true, Pid: 42, Epoch: 3, LeaseExpiresAt: null,
        new ProfileStatus(Approved: null, Live: null, ChangedSinceApproval: false, Problem: null, Source: null, NeedsApproval: false), [])
    {
        ApiVersion = 1,
        Views = new ViewsStatus(Cursor: 3, LedgerHead: 7, ProjectedAt: KnownInstants.Epoch, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null),
    };

    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes = new(StringComparer.Ordinal)
    {
        ["/sindri/status"] = (HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(StatusAtHead7, SliceHarness.Json))),   // every view read asks for the head first
    };
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _posts = new(StringComparer.Ordinal);
    private long? _cursor;
    public List<Call> Calls { get; } = [];

    /// <summary>Answers 503 SND-API-011 to a call whose X-Sindri-After is past <paramref name="cursor"/>, as the server does once its views stay behind.</summary>
    public RoutedApi HonouringAfter(long cursor)
    {
        _cursor = cursor;
        return this;
    }

    public RoutedApi On<T>(string path, HttpStatusCode status, T body)
    {
        _routes[path] = (status, Encoding.UTF8.GetString(AwJson.Serialize(body, SliceHarness.Json)));
        return this;
    }

    /// <summary>Answers a POST to <paramref name="path"/> with its own body, where the same path answers a GET differently.</summary>
    public RoutedApi OnPost<T>(string path, HttpStatusCode status, T body)
    {
        _posts[path] = (status, Encoding.UTF8.GetString(AwJson.Serialize(body, SliceHarness.Json)));
        return this;
    }

    public RoutedApi OnRaw(string path, HttpStatusCode status, string body)
    {
        _routes[path] = (status, body);
        return this;
    }

    /// <summary>Every header that goes out on the wire: the request's and its content's.</summary>
    private static Dictionary<string, string> Sent(HttpRequestMessage request) =>
        request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(h => h.Key, h => string.Join(',', h.Value), StringComparer.OrdinalIgnoreCase);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;   // why: HttpClient always sets the URI
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add(new Call(request.Method.Method, uri.AbsolutePath, uri.Query, body, Sent(request)));
        if (_cursor is { } cursor && request.Headers.TryGetValues(SindriHeaders.After, out var after) && long.Parse(after.Single(), System.Globalization.CultureInfo.InvariantCulture) > cursor)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"type":"about:blank","title":"SND-API-011","status":503,"detail":"views are behind"}""", Encoding.UTF8, "application/problem+json"),
            };
        }

        var (status, text) = request.Method == HttpMethod.Post && _posts.TryGetValue(uri.AbsolutePath, out var posted) ? posted
            : _routes.TryGetValue(uri.AbsolutePath, out var found) ? found : (HttpStatusCode.NotFound, """{"title":"SND-API-404","detail":"no route"}""");
        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") };
    }
}
