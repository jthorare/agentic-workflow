using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ScanRepos;

/// <summary>
/// Looks for repos under a folder of the home (GET /sindri/setup/repos, first-run spec §3.3). A session Sindri launched is refused (SND-AUTH-002): serve runs outside its sandbox and would read the home's layout for it;
/// the terminal, the app and a session the owner started may scan. The root must be the home folder or under it, and readable (SND-SETUP-008); the depth is 1 to 6. The walk itself starts when the rows are read.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="tree">The file system.</param>
/// <param name="known">The account's recent repos.</param>
/// <param name="live">The live profile, for 'inProfile'.</param>
internal sealed class ScanReposService(ICallerContextAccessor callers, IRepoScanTree tree, IKnownRepos known, IProfileSource live) : IService<ScanReposRequest, ScanReposResponse>
{
    private const string Fix = "(fix: sindri setup scan --root DIR)";

    public async ValueTask<Result<ScanReposResponse>> Handle(ScanReposRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Launch is not null)
        {
            return SindriErrors.AuthRefused.With("a session Sindri launched can't scan the home folder (fix: run sindri setup scan from your own terminal)");
        }

        var depth = request.Depth ?? RepoScanRules.DefaultDepth;
        if (depth is < 1 or > RepoScanRules.MaxDepth)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"the depth must be 1 to {RepoScanRules.MaxDepth}, not {depth} (fix: sindri setup scan --depth 3)"));
        }

        var root = Root(request.Root);
        if (root.IsError)
        {
            return root.Error;
        }

        var scan = new RepoScan(tree, known, root.Value, depth, await ProfilePaths(cancellationToken).ConfigureAwait(false));
        return new ScanReposResponse(scan.Rows(cancellationToken), scan.End);   // the endpoint's read adds the client's own token
    }

    private Result<string> Root(string? asked)
    {
        var home = tree.Home;
        var path = string.IsNullOrEmpty(asked) || string.Equals(asked, "~", StringComparison.Ordinal) ? home
            : asked.StartsWith("~/", StringComparison.Ordinal) ? home + asked[1..]
            : asked;
        if (!path.StartsWith('/'))
        {
            return Unreadable(path, "it isn't an absolute path");
        }

        return tree.Real(path) is not { } real ? Unreadable(path, "it is missing or can't be read")
            : RepoScanRules.InsideHome(real, home) ? real
            : Unreadable(path, "it is outside your home folder");
    }

    private static Error Unreadable(string path, string reason) => SindriErrors.SetupScanRootInvalid.With($"couldn't read {path}: {reason} {Fix}");

    // why: a profile that is missing or doesn't load only means no row is in it; the scan stays read-only and answers
    private async ValueTask<IReadOnlySet<string>> ProfilePaths(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError
            ? new HashSet<string>(StringComparer.Ordinal)
            : (loaded.Value.Profile.Repos ?? []).Select(entry => tree.Real(entry.Path) ?? entry.Path.TrimEnd('/')).ToHashSet(StringComparer.Ordinal);
    }
}
