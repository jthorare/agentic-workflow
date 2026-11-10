using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Guards;

/// <summary>
/// The loop guards of a workflow that starts a workflow: a call may be at most 'workflow.callDepth' (default 3) deep, and a run may not start its own workflow, nor any workflow up its call chain.
/// A top-level run is depth 0, the run it calls is 1, and so on, so depth 3 is allowed and 4 is refused. The depth and the caller travel in the callee's trigger as
/// 'callDepth' and 'parentRun', and the workflows up the chain as 'ancestors' (a trigger without the field falls back to the caller and its 'parentRun').
/// </summary>
public static class CallDepth
{
    /// <summary>The depth 'workflow.callDepth' allows when the profile says nothing.</summary>
    public const int DefaultMax = 3;

    /// <summary>Decides whether a run may start <paramref name="target"/>.</summary>
    /// <param name="caller">The calling run.</param>
    /// <param name="target">The slug of the workflow to start.</param>
    /// <param name="max">The deepest call allowed.</param>
    /// <param name="launchLineage">
    /// The slugs of the workflows the calling run's launch descends from, or null. Seam for PR 7: a session's own writes carry its run, so a session that starts a workflow
    /// can be traced back to the workflow that launched it; until then only the trigger's 'parentRun' is read.
    /// </param>
    /// <returns>The callee's depth, SND-GRAPH-121 when the run would start its own workflow, or SND-GRAPH-120 when the call is too deep.</returns>
    public static Result<int> Next(CallOrigin caller, string target, int max, IReadOnlyCollection<string>? launchLineage = null)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(target);
        if (SelfStart(caller, target, launchLineage) is { } own)
        {
            return SindriErrors.GraphSelfStart.With(own);
        }

        var next = caller.Depth + 1;
        return next > max
            ? SindriErrors.GraphCallDepth.With(string.Create(CultureInfo.InvariantCulture, $"calling '{target}' would be depth {next}, and workflow.callDepth allows {max} (fix: start the work from a top-level workflow, or raise workflow.callDepth)"))
            : next;
    }

    /// <summary>The ancestors the callee's trigger carries: the caller's own ancestors plus the caller's workflow, at most <paramref name="max"/> of them (the newest).</summary>
    /// <param name="caller">The calling run.</param>
    /// <param name="max">The deepest call allowed.</param>
    /// <returns>The slugs up the chain, oldest first.</returns>
    public static IReadOnlyList<string> AncestorsOf(CallOrigin caller, int max)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var chain = (caller.Ancestors ?? []).Append(SlugOf(caller.Run)).ToList();
        return chain.Count > max ? chain.GetRange(chain.Count - max, max) : chain;
    }

    /// <summary>The slug part of a run id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'.</summary>
    /// <param name="run">The run id text.</param>
    /// <returns>The slug; the whole text when it has no '@'.</returns>
    public static string SlugOf(string run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var at = run.IndexOf('@', StringComparison.Ordinal);
        return at < 0 ? run : run[..at];
    }

    private static string? SelfStart(CallOrigin caller, string target, IReadOnlyCollection<string>? launchLineage)
    {
        if (string.Equals(SlugOf(caller.Run), target, StringComparison.Ordinal))
        {
            return $"'{target}' can't start itself (fix: call a different workflow)";
        }

        if (caller.Ancestors?.Contains(target, StringComparer.Ordinal) == true)
        {
            return $"'{target}' is up this call chain, so this run can't start it again (fix: end the chain here)";
        }

        if (caller.ParentRun is { } parent && string.Equals(SlugOf(parent), target, StringComparison.Ordinal))
        {
            return $"'{target}' started this run, so this run can't start it again (fix: end the chain here)";
        }

        return launchLineage?.Contains(target, StringComparer.Ordinal) == true ? $"'{target}' launched the session that is calling it (fix: end the chain here)" : null;
    }
}
