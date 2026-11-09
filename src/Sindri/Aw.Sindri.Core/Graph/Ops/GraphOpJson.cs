using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Reads and writes a list of <see cref="GraphOp"/>s as JSON, the form 'workflow_apply_ops' and 'sindri wf edit' use.</summary>
public static class GraphOpJson
{
    /// <summary>Reads a JSON array of ops.</summary>
    /// <param name="json">The text.</param>
    /// <returns>The ops, or SND-GRAPH-012 for text that isn't a list of known ops.</returns>
    public static Result<IReadOnlyList<GraphOp>> Parse(string json) => throw new NotSupportedException("GraphOpJson.Parse is not written yet");

    /// <summary>Writes ops as a JSON array.</summary>
    /// <param name="ops">The ops.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(IReadOnlyList<GraphOp> ops) => throw new NotSupportedException("GraphOpJson.Write is not written yet");
}
