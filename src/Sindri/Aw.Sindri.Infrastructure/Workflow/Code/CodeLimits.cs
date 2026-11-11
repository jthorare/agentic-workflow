namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>What one run of the code may use.</summary>
/// <param name="Timeout">How long it may run before the runner is killed.</param>
/// <param name="MemoryMb">How much memory it may use, in MiB.</param>
internal sealed record CodeLimits(TimeSpan Timeout, int MemoryMb);
