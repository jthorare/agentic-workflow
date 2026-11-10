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

        var working = new GraphEditSession(spec);
        for (var i = 0; i < ops.Count; i++)
        {
            if (working.Apply(ops[i]) is { } problem)
            {
                return SindriErrors.GraphOpInvalid.With($"op {(i + 1).ToString(CultureInfo.InvariantCulture)} ({Kind(ops[i])}): {problem}");
            }
        }

        var edited = working.ToSpec();
        var validation = validator.Validate(edited, GraphMode.Draft);
        return validation.Passed
            ? edited
            : SindriErrors.GraphFindings.With(string.Join("; ", validation.Blocking.Select(static f => f.ToString())));
    }

    private static string Kind(GraphOp op) => op switch
    {
        AddNodeOp => "add_node",
        SetConfigOp => "set_config",
        RemoveNodeOp => "remove_node",
        ConnectOp => "connect",
        DisconnectOp => "disconnect",
        RenameOp => "rename",
        SetMetaOp => "set_meta",
        _ => "unknown",
    };
}
