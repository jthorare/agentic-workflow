using System.Text.Json;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Raw requests with chosen caller headers, and the parts of the answer the caller tests check.</summary>
internal static class CallerRequests
{
    public static HttpRequestMessage Get(string path, params (string Name, string Value)[] headers) => With(new HttpRequestMessage(HttpMethod.Get, path), headers);

    public static HttpRequestMessage Post(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(HttpMethod.Post, path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    public static HttpRequestMessage Put(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(HttpMethod.Put, path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    public static HttpRequestMessage Delete(string path, params (string Name, string Value)[] headers) => With(new HttpRequestMessage(HttpMethod.Delete, path), headers);

    /// <summary>An HTTP QUERY (the IETF safe method with a body, draft): what the subgraph route takes when its keys don't fit a URL.</summary>
    public static HttpRequestMessage Query(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(new HttpMethod("QUERY"), path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    private static readonly AsyncLocal<bool> _humanAsSession = new();

    /// <summary>
    /// While set (on this async flow), <see cref="Human"/> answers the headers of an owner-started agent session instead of the human's, so a suite written
    /// for the human re-runs as a session (PR 2: every verb is open to sessions). Set by <see cref="AsSession"/>.
    /// </summary>
    public static bool HumanAsSession => _humanAsSession.Value;

    /// <summary>Makes <see cref="Human"/> answer a session's headers until the returned scope is disposed.</summary>
    public static IDisposable AsSession()
    {
        _humanAsSession.Value = true;
        return new Scope();
    }

    /// <summary>Makes <see cref="Human"/> answer the human's headers again until the returned scope is disposed: a scenario's seeding inside a session run.</summary>
    public static IDisposable AsHuman()
    {
        var was = _humanAsSession.Value;
        _humanAsSession.Value = false;
        return new Scope(was);
    }

    private static readonly AsyncLocal<string?> _launch = new();

    /// <summary>
    /// Like <see cref="AsSession"/>, and the session also carries <paramref name="launchId"/> in X-Sindri-Launch: a session Sindri itself launched
    /// (the launch must be open: its hash file written). Its writes record the actor 'session:launch:&lt;id&gt;'.
    /// </summary>
    public static IDisposable AsLaunchedSession(string launchId)
    {
        _humanAsSession.Value = true;
        _launch.Value = launchId;
        return new Scope(clearLaunch: true);
    }

    private sealed class Scope(bool restore = false, bool clearLaunch = false) : IDisposable
    {
        public void Dispose()
        {
            _humanAsSession.Value = restore;
            if (clearLaunch)
            {
                _launch.Value = null;
            }
        }
    }

    /// <summary>The repo a session run names (X-Sindri-Repo): the fixture map's, which every context scenario's profile maps; a session must name one for a context call (SND-CONTEXT-021).</summary>
    private const string SessionRepo = FixtureMap.Repo;

    public static (string Name, string Value)[] Human(string? token, string client = "cli") =>
        _humanAsSession.Value ? SessionHeaders(client) :
        token is null
            ? [(SindriHeaders.Caller, "human"), (SindriHeaders.Terminal, "1"), (SindriHeaders.Client, client)]
            : [(SindriHeaders.Caller, "human"), (SindriHeaders.Terminal, "1"), (SindriHeaders.Client, client), (SindriHeaders.Token, token)];

    private static (string Name, string Value)[] SessionHeaders(string client) =>
        _launch.Value is { } launch
            ? [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, client), (SindriHeaders.Repo, SessionRepo), (SindriHeaders.Launch, launch)]
            : [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, client), (SindriHeaders.Repo, SessionRepo)];

    /// <summary>Sends <paramref name="request"/>: the status, the ProblemDetails title (null for another body) and the Sindri-Api-Version header.</summary>
    public static async Task<Answer> Ask(this HttpClient http, HttpRequestMessage request)
    {
        var ct = TestContext.Current.CancellationToken;
        using (request)
        {
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            var version = response.Headers.TryGetValues(SindriHeaders.ApiVersion, out var values) ? string.Join(',', values) : null;
            return new Answer(response.StatusCode, Title(text), version, text);
        }
    }

    private static string? Title(string text)
    {
        try
        {
            return JsonDocument.Parse(text).RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpRequestMessage With(HttpRequestMessage request, (string Name, string Value)[] headers)
    {
        foreach (var (name, value) in headers)
        {
            _ = request.Headers.TryAddWithoutValidation(name, value);
        }

        // why: under AsLaunchedSession a scenario's hand-built session headers (not from Human) are the launched session's too; a call proven by a launch's own secret stays that launch's
        if (_launch.Value is { } launch && headers.Any(static h => h is { Name: SindriHeaders.Caller, Value: "session" })
            && !headers.Any(static h => h.Name is SindriHeaders.Launch or SindriHeaders.LaunchSecret))
        {
            _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Launch, launch);
        }

        return request;
    }
}
