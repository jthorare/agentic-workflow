namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>What one run of the code may use.</summary>
/// <param name="Timeout">How long it may run before the runner is killed.</param>
/// <param name="MemoryMb">How much memory it may use, in MiB.</param>
internal sealed record CodeLimits(TimeSpan Timeout, int MemoryMb)
{
    /// <summary>The most stdout bytes (the result) a run may give: a larger result fails the node with SND-CODE-004.</summary>
    public const int MaxResultBytes = 1024 * 1024;

    /// <summary>The most stderr bytes kept for the error text: the last ones, because the runner's reason is its last line.</summary>
    public const int MaxStderrBytes = 8 * 1024;
}
