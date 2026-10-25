namespace Aw.Sindri.Host.Mcp;

/// <summary>One page of every top-level list in a tool answer. The same limit and offset apply to each list on its own.</summary>
/// <param name="Limit">How many items each list keeps.</param>
/// <param name="Offset">How many items each list skips.</param>
internal sealed record McpWindow(int Limit, int Offset)
{
    /// <summary>How many items each list keeps when a call names no limit.</summary>
    internal const int DefaultLimit = 100;
}
