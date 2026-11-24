using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Every installed tool compared with what origin/main would install, for health (issues #3698, #4249): heavy and meta-gate-reserve
/// by content, aw-meta, judge and sindri by the commit they were built from. Nothing is rebuilt or reinstalled. The builds'
/// comparison (git log and merge-base) is kept under a key of origin/main's commit and the installed builds, and reused while that
/// key holds; a result with an unknown tool is never kept, so a passing git failure is asked again on the next run.
/// </summary>
/// <param name="scripts">Compares heavy and meta-gate-reserve.</param>
/// <param name="builds">Compares aw-meta, judge and sindri.</param>
/// <param name="git">Reads origin/main.</param>
/// <param name="values">Keeps the last comparison of the builds.</param>
/// <param name="paths">Names the toolkit checkout.</param>
internal sealed class ToolDriftCheck(ScriptStaleCheck scripts, BuildDriftCheck builds, ToolkitGit git, IKeyValueStore values, MetaPaths paths)
{
    /// <summary>Compares every installed tool.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One drift per tool; null when there is no toolkit checkout to compare with.</returns>
    internal async ValueTask<IReadOnlyList<ToolDrift>?> Drifts(CancellationToken cancellationToken)
    {
        if (paths.Toolkit is not { Length: > 0 } toolkit)
        {
            return null;
        }

        var found = new List<ToolDrift>(await scripts.Drifts(cancellationToken).ConfigureAwait(false));
        var main = await git.Line(toolkit, ["rev-parse", "origin/main"], cancellationToken).ConfigureAwait(false);
        if (main is null)
        {
            found.AddRange(BuildDriftCheck.Tools.Select(tool => new ToolDrift(tool, DriftState.Unknown, $"no origin/main in {toolkit} to compare with; fix: git -C {toolkit} fetch origin")));
            return found;
        }

        found.AddRange(await Builds(toolkit, main, cancellationToken).ConfigureAwait(false));
        return found;
    }

    private async ValueTask<IReadOnlyList<ToolDrift>> Builds(string toolkit, string main, CancellationToken cancellationToken)
    {
        var key = main + ";" + await builds.Identity(cancellationToken).ConfigureAwait(false);
        var kept = await values.Get(KvKeys.HealthDriftKey, cancellationToken).ConfigureAwait(false);
        var lines = await values.Get(KvKeys.HealthDriftLines, cancellationToken).ConfigureAwait(false);
        if (kept.IsSuccess && string.Equals(kept.Value, key, StringComparison.Ordinal) && lines.IsSuccess && Parse(lines.Value) is { } cached)
        {
            return cached;
        }

        var fresh = await builds.Drifts(toolkit, cancellationToken).ConfigureAwait(false);
        if (fresh.All(static d => d.State != DriftState.Unknown))
        {
            // A store that can't be written only costs the next run its git calls.
            _ = await values.Set(KvKeys.HealthDriftLines, Format(fresh), cancellationToken).ConfigureAwait(false);
            _ = await values.Set(KvKeys.HealthDriftKey, key, cancellationToken).ConfigureAwait(false);
        }

        return fresh;
    }

    private static string Format(IReadOnlyList<ToolDrift> drifts) =>
        string.Join('\n', drifts.Select(static d => d.Tool + "\t" + ((int)d.State).ToString(CultureInfo.InvariantCulture) + "\t" + d.Detail));

    // The kept lines; null when there are none or one doesn't parse, so the comparison runs again.
    private static List<ToolDrift>? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var drifts = new List<ToolDrift>();
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var state) || !Enum.IsDefined((DriftState)state))
            {
                return null;
            }

            drifts.Add(new ToolDrift(parts[0], (DriftState)state, parts[2]));
        }

        return drifts;
    }
}
