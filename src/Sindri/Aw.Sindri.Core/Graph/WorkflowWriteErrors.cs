using System.Globalization;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph;

/// <summary>What the workflow writes (create, edit by ops, replace) share about the command gate they take themselves: the busy answer and the stale-revision answer they give after the re-check inside it.</summary>
public static class WorkflowWriteErrors
{
    /// <summary>The answer when the gate stayed busy for 5 s, as a gated command gets it (SND-API-009).</summary>
    public static Error Busy { get; } = SindriErrors.CommandBusy.With("another command held the command gate for 5 s; try again in a moment");

    /// <summary>The answer when another write made a newer revision between the draft check and the write.</summary>
    /// <param name="latest">The revision now.</param>
    /// <param name="baseRevision">The revision the caller edited.</param>
    /// <param name="what">What the caller made on that revision ("these ops", "this replace").</param>
    /// <returns>SND-GRAPH-007 with the current revision.</returns>
    public static Error Stale(int latest, int baseRevision, string what)
    {
        var current = latest.ToString(CultureInfo.InvariantCulture);
        return SindriErrors.GraphStaleRevision
            .With($"This workflow is at revision {current}, and {what} was made on revision {baseRevision.ToString(CultureInfo.InvariantCulture)}. Read it again (current revision {current}) and retry.")
            .WithExtension("currentRevision", JsonDocument.Parse(current).RootElement.Clone());
    }
}
