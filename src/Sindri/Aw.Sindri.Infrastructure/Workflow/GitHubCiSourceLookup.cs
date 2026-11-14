using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Finds the CI a starter template can read (#3534): a connected GitHub connection plus the first profile repo's github.com origin and default branch.
/// Every miss is null (no connection, no repo, no origin, an origin that is not github.com, a git error): creating a workflow from a template never refuses over this.
/// </summary>
/// <param name="connections">The connections.</param>
/// <param name="profile">The approved profile, for the first repo's path.</param>
internal sealed class GitHubCiSourceLookup(IConnectionStore connections, IApprovedProfile profile) : ICiSourceLookup
{
    private const string Host = "github.com";
    private static readonly string[] _schemes = ["https", "http", "ssh"];

    public async ValueTask<GitHubCiSource?> Find(CancellationToken cancellationToken)
    {
        try
        {
            var listed = await connections.List(cancellationToken).ConfigureAwait(false);
            var connection = listed.IsError
                ? null
                : listed.Value.Where(static c => string.Equals(c.Preset, "github", StringComparison.Ordinal) && c.Status == ConnectionStatus.Connected)
                    .OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).FirstOrDefault();
            if (connection is null)
            {
                return null;
            }

            var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
            if (latest.IsError || latest.Value is not { Profile.Repos: [var first, ..] })
            {
                return null;
            }

            var origin = await GitProcess.Run(first.Path, ["config", "--get", "remote.origin.url"], cancellationToken, hardened: true).ConfigureAwait(false);
            var parsed = origin.Exit == 0 ? ParseRemote(origin.Output) : null;
            if (parsed is not { } found)
            {
                return null;
            }

            return new GitHubCiSource(connection.Name.ToString(), found.Owner, found.Repo, await DefaultBranch(first.Path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is not OperationCanceledException) // boundary: the lookup is a convenience; any failure gives the plain template
        {
            return null;
        }
    }

    /// <summary>Reads owner and repo from an origin URL: https://github.com/o/r(.git), ssh://git@github.com/o/r(.git) or git@github.com:o/r(.git).</summary>
    /// <param name="remoteUrl">The origin URL.</param>
    /// <returns>The owner and repo, or null for any other host or shape.</returns>
    internal static (string Owner, string Repo)? ParseRemote(string remoteUrl)
    {
        ArgumentNullException.ThrowIfNull(remoteUrl);
        var text = remoteUrl.Trim().TrimEnd('/');
        string path;
        if (text.StartsWith("git@" + Host + ":", StringComparison.OrdinalIgnoreCase))
        {
            path = text[("git@" + Host + ":").Length..];
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && _schemes.Contains(uri.Scheme, StringComparer.Ordinal) && string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
        {
            path = uri.AbsolutePath.Trim('/');
        }
        else
        {
            return null;
        }

        if (path.EndsWith(".git", StringComparison.Ordinal))
        {
            path = path[..^".git".Length];
        }

        var parts = path.Split('/');
        return parts is [{ Length: > 0 } owner, { Length: > 0 } repo] ? (owner, repo) : null;
    }

    private static async Task<string> DefaultBranch(string repository, CancellationToken cancellationToken)
    {
        var head = await GitProcess.Run(repository, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], cancellationToken, hardened: true).ConfigureAwait(false);
        return head.Exit == 0 && head.Output.StartsWith("origin/", StringComparison.Ordinal) && head.Output.Length > "origin/".Length ? head.Output["origin/".Length..] : "main";
    }
}
