using Aw.Connectors.Core.GitHub;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The GitHub record feed the sync driver picks up (owned-index spec §5.2): when the profile lists GitHub repos and its token pointer resolves, a <see cref="ConnectorFeed"/> over the GitHub connector, the first pull reaching back by 'context.index.windowDays'.
/// No entry, no pointer or a pointer that doesn't resolve gives no feed (the source's status row says why).
/// </summary>
/// <param name="pointers">Resolves the token pointer.</param>
/// <param name="connectors">Makes the connector.</param>
/// <param name="connections">The Connections page's rows (the GitHub connection whose token the feed reads with when the profile has no pointer).</param>
/// <param name="connectionSecrets">Reads a connection's tokens.</param>
/// <param name="readers">Makes the GitHub reader for a host, the private-host switch and the token.</param>
internal sealed class GitHubRecordFeeds(ISecretPointers pointers, IGitHubConnectorFactory connectors, IConnectionStore connections, IConnectionSecrets connectionSecrets, Func<string?, bool, string, IGitHubReader> readers) : IExternalRecordFeeds
{
    /// <inheritdoc />
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (settings.GitHub is not { Repos.Count: > 0 } github || Token(github.Token, connections, connectionSecrets, pointers) is not { } token
            || GitHubSettings.Create(github.Repos, github.Token, github.Host, github.AllowPrivateHost, commentCap: null) is not { IsSuccess: true } checkedSettings)
        {
            return [];
        }

        var connector = connectors.Create(checkedSettings.Value, readers(github.Host, github.AllowPrivateHost, token), TimeSpan.FromDays(settings.Index.WindowDays));
        return [new ConnectorFeed(SourceId.From("github"), connector, new ProfileRepoMap(github.Repos, repos.Select(static r => r.Name)).Place, scrubbers, settings.LinearTeams)];
    }

    // why: the profile's pointer wins when it resolves (the owner chose it); otherwise the GitHub connection's token, so Reviews read with the credential the Connections page signed in
    private static string? Token(string? pointer, IConnectionStore connections, IConnectionSecrets connectionSecrets, ISecretPointers pointers)
    {
        if (pointer is not null && SecretPointer.Parse(pointer) is { IsSuccess: true } parsed && pointers.Resolve(parsed.Value) is { IsSuccess: true } resolved)
        {
            return resolved.Value;
        }

#pragma warning disable RS0030 // IExternalRecordFeeds.For is synchronous and the connection list is one local SQLite read; the host has no synchronization context to deadlock on
        var listed = connections.List(CancellationToken.None).AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
        if (listed.IsError)
        {
            return null;
        }

        var connection = listed.Value.Where(static c => c.Preset == "github" && c.Status == ConnectionStatus.Connected && c.SecretPointer is not null).OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).FirstOrDefault();
        return connection?.SecretPointer is { } location && connectionSecrets.Read(location) is { IsSuccess: true } tokens ? tokens.Value.AccessToken : null;
    }
}
