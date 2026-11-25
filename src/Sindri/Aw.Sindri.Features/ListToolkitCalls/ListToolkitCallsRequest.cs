using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListToolkitCalls;

/// <summary>The newest toolkit calls ('sindri toolkit calls'; MCP toolkit_calls).</summary>
/// <param name="Toolkit">Only this toolkit's calls, or all when null.</param>
/// <param name="Limit">The most calls to return (default 50).</param>
public sealed record ListToolkitCallsRequest(string? Toolkit = null, int Limit = ListToolkitCallsRequest.DefaultLimit) : IQuery<ListToolkitCallsResponse>
{
    /// <summary>The limit when none is given.</summary>
    public const int DefaultLimit = 50;
}
