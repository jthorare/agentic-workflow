using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Connections.GitHub;

/// <summary>
/// <see cref="IKnownRepos"/> over the Connections page (first-run spec gap 7): one GET https://api.github.com/user/repos?sort=pushed&amp;per_page=100 with the first connected 'github' connection's token (https,
/// api.github.com only, no redirect followed); the 100 repos the account pushed to most lately are the known ones. No connected account (or no token) is no slug and no error. The token is in no error or log.
/// </summary>
/// <param name="store">The Connections page's rows.</param>
/// <param name="secrets">Reads a connection's tokens.</param>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="credentials">Reads the picked connection's token: a gh login's `command:` credential or the secret pointer (first-run spec decision 14).</param>
internal sealed class GitHubKnownRepos(IConnectionStore store, IConnectionSecrets secrets, HttpClient http, IConnectionCredentials credentials) : IKnownRepos
{

    private const string Preset = "github";
    private static readonly Uri _recent = new("https://api.github.com/user/repos?sort=pushed&per_page=100&affiliation=owner,collaborator,organization_member");

    public async ValueTask<Result<IReadOnlySet<string>>> Slugs(CancellationToken cancellationToken)
    {
        var listed = await store.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        GC.KeepAlive(credentials);   // not read yet (Part of #3321): the readers' red
        var connected = listed.Value.Where(static c => string.Equals(c.Preset, Preset, StringComparison.Ordinal) && c.Status == ConnectionStatus.Connected)
            .OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).FirstOrDefault();
        return connected?.SecretPointer is { } pointer && secrets.Read(pointer) is { IsSuccess: true } tokens && tokens.Value.AccessToken is { Length: > 0 } token
            ? await Read(token, cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    }

    private async ValueTask<Result<IReadOnlySet<string>>> Read(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _recent);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("sindri");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode == HttpStatusCode.Unauthorized
                    ? SindriErrors.ConnectionNeedsSignIn.With("GitHub refused the token for the recent repos")
                    : SindriErrors.ConnectionProviderChanged.With(string.Create(CultureInfo.InvariantCulture, $"GitHub answered {(int)response.StatusCode} for the recent repos"));
            }

            return await Parse(response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return SindriErrors.ConnectionProviderChanged.With("GitHub's recent repos couldn't be read: " + e.GetType().Name);
        }
    }

    private static async ValueTask<Result<IReadOnlySet<string>>> Parse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (body.RootElement.ValueKind != JsonValueKind.Array)
            {
                return SindriErrors.ConnectionProviderChanged.With("GitHub's recent repos weren't a list");
            }

            var slugs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var repo in body.RootElement.EnumerateArray())
            {
                if (repo.ValueKind == JsonValueKind.Object && repo.TryGetProperty("full_name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { Length: > 0 } slug)
                {
                    _ = slugs.Add(slug.ToLowerInvariant());
                }
            }

            return Results.Success<IReadOnlySet<string>>(slugs);
        }
    }
}
