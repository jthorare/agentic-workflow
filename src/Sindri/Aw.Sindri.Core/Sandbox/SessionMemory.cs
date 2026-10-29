namespace Aw.Sindri.Core.Sandbox;

/// <summary>A session's memory use against its limit.</summary>
/// <param name="UsedBytes">memory.current of the session's scope, or null when the scope is gone.</param>
/// <param name="MaxBytes">The plan's memory ceiling in bytes, or null.</param>
public sealed record SessionMemory(long? UsedBytes, long? MaxBytes);
