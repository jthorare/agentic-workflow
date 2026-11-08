namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The size limits of a node run's stored text.</summary>
public static class NodeRunLimits
{
    /// <summary>A node's input and its output are each stored up to 64 KB (UTF-8 bytes); the rest is cut and 'Truncated' is set.</summary>
    public const int PayloadBytes = 64 * 1024;
}
