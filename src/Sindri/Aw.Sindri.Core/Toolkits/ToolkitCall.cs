namespace Aw.Sindri.Core.Toolkits;

/// <summary>One logged call of a toolkit tool: the payloads are scrubbed and capped before a row holds them.</summary>
/// <param name="Toolkit">The toolkit name.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Caller">Who called: a session, a workflow node or the human.</param>
/// <param name="Input">The scrubbed, capped arguments JSON.</param>
/// <param name="Output">The scrubbed, capped result text.</param>
/// <param name="Truncated">True when the input or the output was cut at <see cref="ToolkitCallLog.MaxPayloadChars"/>.</param>
/// <param name="Error">The scrubbed error text of a failed call.</param>
/// <param name="DurationMs">How long the call took.</param>
/// <param name="At">When the call started.</param>
public sealed record ToolkitCall(string Toolkit, string Tool, string Caller, string? Input, string? Output, bool Truncated, string? Error, long DurationMs, DateTimeOffset At);
