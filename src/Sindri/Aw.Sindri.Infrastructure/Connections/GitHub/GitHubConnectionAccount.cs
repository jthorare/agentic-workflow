using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections.GitHub;

/// <summary>
/// <see cref="IGitHubAccount"/> over the Connections page: the first connected 'github' connection (by name) is the account, else one that needs sign-in, else none.
/// The login is the 'login' of GET https://api.github.com/user sent with the connection's token (https only, api.github.com only, no redirect followed). It is kept per connection until
/// the connection's refresh or update time changes; a failed call gives a null login with the reason (<see cref="GitHubLoginMiss"/>) and is not asked again for a minute. The token is in no error, log or exception.
/// </summary>
/// <param name="store">The Connections page's rows.</param>
/// <param name="secrets">Reads a connection's tokens.</param>
/// <param name="presets">The presets (the GitHub probe URL).</param>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="clock">The clock.</param>
internal sealed class GitHubConnectionAccount(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, HttpClient http, TimeProvider clock) : IGitHubAccount
{
    private const string Host = "api.github.com";
    private const string Preset = "github";
    private static readonly TimeSpan _retryAfter = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Known> _known = new(StringComparer.Ordinal);

    public async ValueTask<Result<GitHubAccount>> Current(CancellationToken cancellationToken)
    {
        var listed = await store.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var github = listed.Value.Where(static c => string.Equals(c.Preset, Preset, StringComparison.Ordinal)).OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).ToList();
        if (github.FirstOrDefault(static c => c.Status == ConnectionStatus.Connected) is { } connected)
        {
            var known = await Login(connected, cancellationToken).ConfigureAwait(false);
            return new GitHubAccount(GitHubAccountState.Connected, connected.Name.ToString(), known.Login, known.Miss);
        }

        return github.FirstOrDefault(static c => c.Status == ConnectionStatus.NeedsSignIn) is { } stale
            ? new GitHubAccount(GitHubAccountState.NeedsSignIn, stale.Name.ToString(), Login: null, GitHubLoginMiss.SignIn)
            : new GitHubAccount(GitHubAccountState.Missing, Connection: null, Login: null);
    }

    private async ValueTask<Known> Login(Connection connection, CancellationToken cancellationToken)
    {
        var key = connection.Name.ToString();
        var stamp = connection.LastRefreshedAt ?? connection.UpdatedAt;
        var now = clock.GetUtcNow();
        if (_known.TryGetValue(key, out var known) && known.Stamp == stamp && (known.Login is not null || now - known.At < _retryAfter))
        {
            return known;
        }

        var (login, miss) = await Ask(connection, cancellationToken).ConfigureAwait(false);
        var fresh = new Known(stamp, login, miss, now);
        _known[key] = fresh;
        return fresh;
    }

    private async ValueTask<(string? Login, GitHubLoginMiss Miss)> Ask(Connection connection, CancellationToken cancellationToken)
    {
        var url = connection.Config.GetValueOrDefault("probeUrl") ?? presets.Find(Preset)?.ProbeUrl;
        if (GitHubUser(url) is not { } uri)
        {
            return (null, GitHubLoginMiss.UnsupportedHost);
        }

        return Token(connection) is { } token
            ? await Send(uri, token, cancellationToken).ConfigureAwait(false)
            : (null, GitHubLoginMiss.SignIn);
    }

    private string? Token(Connection connection) =>
        connection.SecretPointer is { } pointer && secrets.Read(pointer) is { IsSuccess: true } tokens ? tokens.Value.AccessToken : null;

    private async ValueTask<(string? Login, GitHubLoginMiss Miss)> Send(Uri uri, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("sindri");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await ReadLogin(response, cancellationToken).ConfigureAwait(false) is { } login ? (login, GitHubLoginMiss.None) : (null, GitHubLoginMiss.Unreachable)
                : (null, response.StatusCode == HttpStatusCode.Unauthorized ? GitHubLoginMiss.SignIn : GitHubLoginMiss.Unreachable);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return (null, GitHubLoginMiss.Unreachable);
        }
    }

    // why: the token goes only to api.github.com over https; a probe URL anywhere else is not asked
    private static Uri? GitHubUser(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps && string.Equals(parsed.Host, Host, StringComparison.OrdinalIgnoreCase) ? parsed : null;

    private static async ValueTask<string?> ReadLogin(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String && login.GetString() is { Length: > 0 } text ? text : null;
        }
    }

    private sealed record Known(DateTimeOffset Stamp, string? Login, GitHubLoginMiss Miss, DateTimeOffset At);
}
