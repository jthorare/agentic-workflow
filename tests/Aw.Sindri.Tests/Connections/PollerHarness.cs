using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>A poller wired to a <see cref="FakeProvider"/>, a connection whose token the fake keychain holds, and one event kind of that preset.</summary>
/// <param name="Poller">The poller under test.</param>
/// <param name="Connection">The connection (its secret pointer names a stored token).</param>
/// <param name="Kind">An event kind of the preset.</param>
/// <param name="Http">The client over the fake; the caller disposes it.</param>
internal sealed record PollerHarness(IEventPoller Poller, Connection Connection, EventKind Kind, HttpClient Http) : IDisposable
{
    /// <summary>Disposes the client over the fake.</summary>
    public void Dispose() => Http.Dispose();

    /// <summary>The access token stored for the connection.</summary>
    public const string Token = "tok-123";

    /// <summary>Builds the harness for a preset.</summary>
    /// <param name="preset">'github' or 'linear'.</param>
    /// <param name="provider">The fake provider.</param>
    /// <returns>The harness.</returns>
    public static PollerHarness For(string preset, FakeProvider provider)
    {
        var secrets = new FakeConnectionSecrets();
        var name = ConnectionSamples.Name(preset, "work");
        _ = secrets.Store(name, new TokenSet(Token, RefreshToken: null, ExpiresAt: null));
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["repo"] = "acme/app" };
        var connection = ConnectionSamples.Oauth(name) with { Config = config };
        var http = new HttpClient(provider, disposeHandler: false);
        return preset == "github"
            ? new PollerHarness(new GitHubEventPoller(http, secrets), connection, EventKind.GitHubPrMerged, http)
            : new PollerHarness(new LinearEventPoller(http, secrets), connection, EventKind.LinearIssueLabeled, http);
    }
}
