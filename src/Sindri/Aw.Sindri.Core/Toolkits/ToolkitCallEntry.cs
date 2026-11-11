namespace Aw.Sindri.Core.Toolkits;

/// <summary>A toolkit call as it happened, before the log scrubs and caps it.</summary>
/// <param name="Toolkit">The toolkit name.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Caller">Who called: a session, a workflow node or the human.</param>
/// <param name="Input">The arguments JSON.</param>
/// <param name="Output">The result text.</param>
/// <param name="Error">The error text of a failed call.</param>
/// <param name="Duration">How long the call took.</param>
/// <param name="StartedAt">When it started.</param>
public sealed record ToolkitCallEntry(string Toolkit, string Tool, string Caller, string? Input, string? Output, string? Error, TimeSpan Duration, DateTimeOffset StartedAt);
