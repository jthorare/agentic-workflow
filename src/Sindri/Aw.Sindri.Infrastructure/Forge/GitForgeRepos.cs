using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Forge;

/// <summary>
/// <see cref="IForgeRepos"/> from the workspace's 'origin' remote ('git -C workspace remote get-url origin'), as an https, ssh:// or scp-style url
/// ('https://github.com/owner/name.git', 'git@github.com:owner/name.git').
/// </summary>
/// <param name="runner">Runs git.</param>
/// <param name="profile">The approved profile: only its 'workflow.forge.hosts' are named.</param>
internal sealed class GitForgeRepos(IToolRunner runner, IApprovedProfile profile) : IForgeRepos
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public async ValueTask<Result<PullRepo>> OfWorkspace(string workspace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var run = runner.Run("git", ["-C", workspace, "remote", "get-url", "origin"], _timeout, options: null, cancellationToken);
        if (run.Outcome != ToolOutcome.Exited || run.ExitCode != 0 || Parse(run.Output.Trim()) is not { } repo)
        {
            return SindriErrors.NoForge.With($"{workspace} has no 'origin' remote that names a forge repository (fix: git remote add origin <url>)");
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var hosts = approved.Value?.Profile.Workflow?.Forge?.Hosts ?? [];
        return hosts.Contains(repo.Host, StringComparer.OrdinalIgnoreCase)
            ? repo
            : SindriErrors.ForgeRefused.With($"{repo.Host} is not on the profile's workflow.forge.hosts (fix: add it and approve the profile)");
    }

    /// <summary>The repository a remote url names; null for a url that isn't host/owner/name.</summary>
    /// <param name="url">The remote url.</param>
    /// <returns>The repository, or null.</returns>
    internal static PullRepo? Parse(string url)
    {
        string host;
        string path;
        if (url.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            (host, path) = (uri.Host, uri.AbsolutePath.Trim('/'));
        }
        else if (url.IndexOf(':', StringComparison.Ordinal) is var colon and > 0 && !url.Contains("://", StringComparison.Ordinal))
        {
            (host, path) = (url[..colon][(url.IndexOf('@', StringComparison.Ordinal) + 1)..], url[(colon + 1)..].Trim('/'));
        }
        else
        {
            return null;
        }

        var parts = (path.EndsWith(".git", StringComparison.Ordinal) ? path[..^4] : path).Split('/');
        return host.Length > 0 && parts is [{ Length: > 0 } owner, { Length: > 0 } name] ? new PullRepo(host, owner, name) : null;
    }
}
