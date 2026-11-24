using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Compares aw-meta, judge and sindri with origin/main's last change to their inputs (issue #4249). A build is current when that
/// change is an ancestor of, or equal to, the commit it was built from: scripts/meta-version-lib.sh's rule for aw-meta, here against
/// origin/main. aw-meta's inputs are scripts/meta-build-paths.txt at origin/main; judge's and sindri's are the pathspecs install-cs
/// recorded in inputs/&lt;name&gt;.paths.
/// </summary>
/// <param name="git">Reads the toolkit checkout.</param>
/// <param name="builds">What install-cs recorded about judge and sindri.</param>
/// <param name="self">This aw-meta's build stamp.</param>
internal sealed class BuildDriftCheck(ToolkitGit git, IInstalledBuilds builds, IBuildInfo self)
{
    private const string InstallCs = "scripts/install-cs.sh --publish --link";
    private const string InstallMeta = "scripts/install-meta.sh";

    /// <summary>The tools this check compares, in order.</summary>
    internal static readonly IReadOnlyList<string> Tools = ["aw-meta", "judge", "sindri"];

    private static readonly string[] _binaries = ["judge", "sindri"];

    /// <summary>What the comparison is made from, for the cache key: aw-meta's commit and each binary's revision and pathspecs.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The identity.</returns>
    internal async ValueTask<string> Identity(CancellationToken cancellationToken)
    {
        var parts = new List<string> { "aw-meta=" + self.Read().Commit };
        foreach (var name in _binaries)
        {
            var record = await builds.Current(name, cancellationToken).ConfigureAwait(false);
            parts.Add(name + "=" + (record.IsSuccess && record.Value is { } built ? built.Revision + "/" + (built.Inputs is { } inputs ? string.Join(",", inputs) : "(no pathspecs)") : "(none)"));
        }

        return string.Join(";", parts);
    }

    /// <summary>Compares each tool.</summary>
    /// <param name="toolkit">The toolkit checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One drift per tool, in <see cref="Tools"/> order.</returns>
    internal async ValueTask<IReadOnlyList<ToolDrift>> Drifts(string toolkit, CancellationToken cancellationToken)
    {
        var drifts = new List<ToolDrift> { await Meta(toolkit, cancellationToken).ConfigureAwait(false) };
        foreach (var name in _binaries)
        {
            drifts.Add(await Binary(toolkit, name, cancellationToken).ConfigureAwait(false));
        }

        return drifts;
    }

    private async ValueTask<ToolDrift> Meta(string toolkit, CancellationToken cancellationToken)
    {
        var commit = self.Read().Commit;
        if (string.Equals(commit, "unknown", StringComparison.Ordinal))
        {
            return new ToolDrift("aw-meta", DriftState.Unknown, "this aw-meta carries no commit stamp (a dev build); fix: " + InstallMeta);
        }

        var list = await git.Text(toolkit, ["show", "origin/main:scripts/meta-build-paths.txt"], cancellationToken).ConfigureAwait(false);
        string[] specs = list is null ? [] : [.. list.Split('\n').Select(static l => l.Trim()).Where(static l => l.Length > 0 && !l.StartsWith('#'))];
        return specs.Length == 0
            ? new ToolDrift("aw-meta", DriftState.Unknown, $"no origin/main:scripts/meta-build-paths.txt in {toolkit}; fix: git -C {toolkit} fetch origin")
            : await Compare(toolkit, new Query("aw-meta", commit, specs, InstallMeta), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ToolDrift> Binary(string toolkit, string name, CancellationToken cancellationToken)
    {
        var record = await builds.Current(name, cancellationToken).ConfigureAwait(false);
        if (!record.IsSuccess || record.Value is not { } built)
        {
            return new ToolDrift(name, DriftState.Unknown, $"no current install-cs build records {name}; fix: {InstallCs}");
        }

        return built.Inputs is { Count: > 0 } specs
            ? await Compare(toolkit, new Query(name, built.Revision, specs, InstallCs), cancellationToken).ConfigureAwait(false)
            : new ToolDrift(name, DriftState.Unknown, $"its build predates inputs/{name}.paths, so its inputs can't be compared; fix: {InstallCs}");
    }

    private async ValueTask<ToolDrift> Compare(string toolkit, Query query, CancellationToken cancellationToken)
    {
        var (tool, revision, specs, fix) = query;
        if (revision.EndsWith("-dirty", StringComparison.Ordinal))
        {
            return new ToolDrift(tool, DriftState.Stale, $"built from uncommitted changes at {Short(revision)}; fix: {fix}");
        }

        var last = await git.Line(toolkit, ["log", "-1", "--format=%H", "origin/main", "--", .. specs], cancellationToken).ConfigureAwait(false);
        if (last is null)
        {
            return new ToolDrift(tool, DriftState.Unknown, $"git could not read origin/main's history of its inputs in {toolkit}");
        }

        var exit = await git.Exit(toolkit, ["merge-base", "--is-ancestor", last, revision], cancellationToken).ConfigureAwait(false);
        return exit switch
        {
            0 => new ToolDrift(tool, DriftState.Current, $"built at {Short(revision)}, at or after origin/main's last change to its inputs ({Short(last)})"),
            1 => new ToolDrift(tool, DriftState.Stale, $"built at {Short(revision)}; origin/main changed its inputs at {Short(last)}; fix: {fix}"),
            _ => new ToolDrift(tool, DriftState.Unknown, $"its build commit {Short(revision)} is not in {toolkit}; fix: git -C {toolkit} fetch origin"),
        };
    }

    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;

    private sealed record Query(string Tool, string Revision, IReadOnlyList<string> Specs, string Fix);
}
