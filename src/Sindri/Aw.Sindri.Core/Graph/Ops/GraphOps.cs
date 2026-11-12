using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Applies a batch of <see cref="GraphOp"/>s to a workflow graph.</summary>
public static class GraphOps
{
    /// <summary>
    /// Applies <paramref name="ops"/> in order to a copy of <paramref name="spec"/>: all of them or none. A <paramref name="baseRevision"/> that is not
    /// <paramref name="currentRevision"/> is SND-GRAPH-007 naming the current revision; an op that can't apply is SND-GRAPH-012 naming the op's number;
    /// a result the validator blocks (draft mode, so unwired nodes pass) is SND-GRAPH-002 listing the findings. <paramref name="spec"/> is never changed.
    /// </summary>
    /// <param name="spec">The graph at <paramref name="currentRevision"/>.</param>
    /// <param name="baseRevision">The revision the caller read before it made these ops.</param>
    /// <param name="currentRevision">The latest revision now.</param>
    /// <param name="ops">The edits, applied in order.</param>
    /// <param name="validator">Checks the edited graph.</param>
    /// <returns>The edited graph, or why nothing changed.</returns>
    public static Result<GraphSpec> Apply(GraphSpec spec, int baseRevision, int currentRevision, IReadOnlyList<GraphOp> ops, GraphValidator validator)
    {
        var edited = ApplyWithGaps(spec, baseRevision, currentRevision, ops, validator);
        return edited.IsError ? edited.Error : edited.Value.Spec;
    }

    /// <summary>
    /// Like <see cref="Apply"/>, and also returns the findings an edit leaves as wiring gaps: a 'replace_node' that swaps the trigger saves even when a step reads a field the new trigger lacks
    /// (GRAPH-REF-*), and each such reference comes back in the Gaps item as a warning (Workflows page spec decision 5). A node that is not the trigger is SND-GRAPH-112.
    /// </summary>
    /// <param name="spec">The graph at <paramref name="currentRevision"/>.</param>
    /// <param name="baseRevision">The revision the caller read before it made these ops.</param>
    /// <param name="currentRevision">The latest revision now.</param>
    /// <param name="ops">The edits, applied in order.</param>
    /// <param name="validator">Checks the edited graph.</param>
    /// <returns>The edited graph and its gaps, or why nothing changed.</returns>
    public static Result<(GraphSpec Spec, IReadOnlyList<GraphFinding> Gaps)> ApplyWithGaps(GraphSpec spec, int baseRevision, int currentRevision, IReadOnlyList<GraphOp> ops, GraphValidator validator)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(validator);
        if (baseRevision != currentRevision)
        {
            var current = currentRevision.ToString(CultureInfo.InvariantCulture);
            return SindriErrors.GraphStaleRevision
                .With($"This workflow is at revision {current}, and these ops were made on revision {baseRevision.ToString(CultureInfo.InvariantCulture)}. Read it again (current revision {current}) and retry.")
                .WithExtension("currentRevision", JsonDocument.Parse(current).RootElement.Clone());
        }

        if (ops.Count == 0)
        {
            return SindriErrors.GraphOpInvalid.With("send at least one op");
        }

        var working = new GraphEditSession(spec, validator.IsTriggerType);
        for (var i = 0; i < ops.Count; i++)
        {
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (working.NotTheTrigger(ops[i]) is { } notTrigger)
            {
                return SindriErrors.GraphOnlyTriggerReplaceable.With($"op {number} (replace_node): {notTrigger} Only the trigger can be replaced.");
            }

            if (working.Apply(ops[i]) is { } problem)
            {
                return SindriErrors.GraphOpInvalid.With($"op {number} ({Kind(ops[i])}): {problem}");
            }
        }

        var edited = working.ToSpec();
        var validation = validator.Validate(edited, GraphMode.Draft);
        var swapped = ops.Any(static o => o is ReplaceNodeOp) ? SwapGaps(spec, validator, validation) : [];
        var refusing = validation.Blocking.Where(f => !swapped.Contains(f)).ToList();
        return refusing.Count == 0
            ? (edited, [.. swapped.Select(static f => f with { Severity = GraphSeverity.Warning, WiringGap = true })])
            : SindriErrors.GraphFindings.With(string.Join("; ", refusing.Select(static f => f.ToString())));
    }

    // why: a swapped trigger may lack a field a step reads; that is a gap the person fixes next, not a refusal. Only references the swap newly broke count, so an old mistake still refuses.
    private static IReadOnlyList<GraphFinding> SwapGaps(GraphSpec before, GraphValidator validator, GraphValidation after)
    {
        var old = validator.Validate(before, GraphMode.Draft).Findings.Select(Key).ToHashSet(StringComparer.Ordinal);
        return [.. after.Blocking.Where(f => f.Code.StartsWith("GRAPH-REF-", StringComparison.Ordinal) && !old.Contains(Key(f)))];
    }

    // why: GraphFinding holds its nodes in a list, so record equality compares the list by reference
    private static string Key(GraphFinding f) => $"{f.Code}|{string.Join(',', f.Nodes)}|{f.Field}|{f.Message}";

    private static string Kind(GraphOp op) => op switch
    {
        AddNodeOp => "add_node",
        SetConfigOp => "set_config",
        RemoveNodeOp => "remove_node",
        ConnectOp => "connect",
        DisconnectOp => "disconnect",
        RenameOp => "rename",
        SetMetaOp => "set_meta",
        ReplaceNodeOp => "replace_node",
        _ => "unknown",
    };
}
