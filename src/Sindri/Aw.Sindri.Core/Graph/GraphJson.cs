using Aw.Kernel;

namespace Aw.Sindri.Core.Graph;

/// <summary>Reads a graph spec from its JSON text and hashes it.</summary>
public static class GraphJson
{
    /// <summary>Reads <paramref name="json"/> as a spec; an unknown member, a wrong type or bad JSON is refused.</summary>
    /// <param name="json">The document.</param>
    /// <returns>The spec, or SND-GRAPH-001 naming what is wrong.</returns>
    public static Result<GraphSpec> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        _ = GraphJsonContext.Default;
        throw new NotSupportedException();
    }

    /// <summary>The spec's hash, 'sha256:' and 64 hex digits over its canonical JSON: the same graph hashes the same whatever its spacing or member order.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The hash.</returns>
    public static string Hash(GraphSpec spec) => throw new NotSupportedException();
}
