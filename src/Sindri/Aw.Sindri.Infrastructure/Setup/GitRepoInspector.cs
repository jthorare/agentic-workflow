using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// <see cref="IRepoInspector"/> over git (setup-once spec §5.1). Every git call is hardened ('-c core.fsmonitor= -c core.hooksPath=/dev/null', 'GIT_CONFIG_NOSYSTEM=1') and takes an argument list. The main checkout is the
/// parent of the git common dir (so a linked worktree resolves to its repository, API spec §3.1), else git's top level (a submodule), resolved to its real path and judged by <see cref="RepoFolders.Refuse"/>.
/// </summary>
/// <param name="settings">Where the Sindri home is.</param>
internal sealed class GitRepoInspector(SindriSettings settings) : IRepoInspector
{
    private const string Fix = "(fix: pick the repo's top folder)";

    public async ValueTask<Result<InspectedRepo>> Inspect(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (RealPath.Of(path) is not { } real)
        {
            return Invalid($"{path} can't be followed (a loop of links)");
        }

        if (!Directory.Exists(real))
        {
            return Invalid("that folder doesn't exist");
        }

        if (!Readable(real))
        {
            return Invalid("that folder can't be read");
        }

        var checkout = await Checkout(real, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return checkout.Error;
        }

        var refused = RepoFolders.Refuse(checkout.Value, RealPath.Of(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? string.Empty, RealPath.Of(settings.Home) ?? settings.Home);
        return refused is not null ? Invalid(refused) : new InspectedRepo(checkout.Value, await Git(checkout.Value, cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask<Result<RepoGit>> Facts(string checkout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        if (!Directory.Exists(checkout))
        {
            return Invalid("that folder doesn't exist");
        }

        var inside = await GitProcess.Run(checkout, ["rev-parse", "--git-common-dir"], cancellationToken, hardened: true).ConfigureAwait(false);
        return inside.Exit == 0 ? await Git(checkout, cancellationToken).ConfigureAwait(false) : Invalid("that folder is not a git checkout");
    }

    private static async ValueTask<Result<string>> Checkout(string real, CancellationToken cancellationToken)
    {
        var common = await GitProcess.Run(real, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken, hardened: true).ConfigureAwait(false);
        if (common.Exit != 0 || common.Output.Length == 0)
        {
            return Invalid("that folder is not a git checkout");
        }

        var gitDir = common.Output.TrimEnd('/');
        var top = string.Equals(Path.GetFileName(gitDir), ".git", StringComparison.Ordinal)
            ? Path.GetDirectoryName(gitDir)
            : (await GitProcess.Run(real, ["rev-parse", "--show-toplevel"], cancellationToken, hardened: true).ConfigureAwait(false)) is { Exit: 0 } toplevel ? toplevel.Output : null;
        return top is { Length: > 0 } && RealPath.Of(top) is { } resolved ? resolved : Invalid("that folder is not a git checkout with a working tree");
    }

    private static async ValueTask<RepoGit> Git(string checkout, CancellationToken cancellationToken)
    {
        var branch = await GitProcess.Run(checkout, ["symbolic-ref", "--short", "-q", "HEAD"], cancellationToken, hardened: true).ConfigureAwait(false);
        var head = await GitProcess.Run(checkout, ["rev-parse", "--short", "HEAD"], cancellationToken, hardened: true).ConfigureAwait(false);
        return new RepoGit(branch is { Exit: 0, Output.Length: > 0 } ? branch.Output : null, head is { Exit: 0, Output.Length: > 0 } ? head.Output : null);
    }

    private static bool Readable(string directory)
    {
        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static Error Invalid(string reason) => SindriErrors.SetupFolderInvalid.With($"{reason} {Fix}");
}
