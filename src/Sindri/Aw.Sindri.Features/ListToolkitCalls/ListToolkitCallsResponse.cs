using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkitCalls;

/// <summary>The calls, newest first.</summary>
/// <param name="Calls">The calls.</param>
public sealed record ListToolkitCallsResponse(IReadOnlyList<ToolkitCall> Calls);
