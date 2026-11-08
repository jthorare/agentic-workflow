using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.Time.Testing;

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
    /// <param name="preset">'github', 'linear', 'slack' or 'google'.</param>
    /// <param name="provider">The fake provider.</param>
    /// <param name="settings">Connection settings to add or override, or null.</param>
    /// <param name="pageCap">The most pages one GitHub or Linear poll reads.</param>
    /// <returns>The harness. The Google poller's clock stands at <see cref="KnownInstants.Epoch"/>.</returns>
    public static PollerHarness For(string preset, FakeProvider provider, IReadOnlyDictionary<string, string>? settings = null, int pageCap = PollLimits.DefaultPageCap)
    {
        var secrets = new FakeConnectionSecrets();
        var name = ConnectionSamples.Name(preset, "work");
        _ = secrets.Store(name, new TokenSet(Token, RefreshToken: null, ExpiresAt: null));
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["repo"] = "acme/app", ["channel"] = "C123", ["filter"] = "deploy", ["userId"] = "UOWNER" };
        foreach (var (key, value) in settings ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            config[key] = value;
        }

        var connection = ConnectionSamples.Oauth(name) with { Config = config };
        var http = new HttpClient(provider, disposeHandler: false);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        return preset switch
        {
            "github" => new PollerHarness(new GitHubEventPoller(http, secrets, clock, pageCap), connection, EventKind.GitHubPrMerged, http),
            "linear" => new PollerHarness(new LinearEventPoller(http, secrets, clock, pageCap), connection, EventKind.LinearIssueLabeled, http),
            "slack" => new PollerHarness(new SlackEventPoller(http, secrets, clock, pageCap), connection, EventKind.SlackMessage, http),
            _ => new PollerHarness(new GoogleCalendarPoller(http, secrets, clock), connection, EventKind.GoogleCalendarEventStarting, http),
        };
    }
}
