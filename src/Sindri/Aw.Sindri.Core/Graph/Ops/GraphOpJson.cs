using System.Text.Json;
using Aw.Kernel;
using Vogen;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Reads and writes a list of <see cref="GraphOp"/>s as JSON, the form 'workflow_apply_ops' and 'sindri wf edit' use.</summary>
public static class GraphOpJson
{
    /// <summary>Reads a JSON array of ops.</summary>
    /// <param name="json">The text.</param>
    /// <returns>The ops, or SND-GRAPH-012 for text that isn't a list of known ops.</returns>
    public static Result<IReadOnlyList<GraphOp>> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        IReadOnlyList<GraphOp>? ops;
        try
        {
            ops = JsonSerializer.Deserialize(json, GraphOpJsonContext.Default.IReadOnlyListGraphOp);
        }
        catch (Exception ex) when (ex is JsonException or ValueObjectValidationException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // why: an id that fails its shape throws from the id's converter, not as a JsonException; Parse never throws on any text.
            return SindriErrors.GraphOpInvalid.With($"the ops can't be read: {ex.Message}");
        }

        if (ops is null || ops.Any(static op => op is null))
        {
            return SindriErrors.GraphOpInvalid.With("the ops must be a JSON array of op objects");
        }

        return Results.Success(ops);
    }

    /// <summary>Writes ops as a JSON array.</summary>
    /// <param name="ops">The ops.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(IReadOnlyList<GraphOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);
        return JsonSerializer.Serialize(ops, GraphOpJsonContext.Default.IReadOnlyListGraphOp);
    }
}
