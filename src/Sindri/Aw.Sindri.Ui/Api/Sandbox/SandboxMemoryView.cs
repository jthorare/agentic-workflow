namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>A session's memory against its ceiling (wire contract item 1).</summary>
/// <param name="UsedBytes">The slice's memory.current; null when the scope is gone.</param>
/// <param name="MaxBytes">The plan's memory limit; null when none.</param>
internal sealed record SandboxMemoryView(long? UsedBytes, long? MaxBytes);
