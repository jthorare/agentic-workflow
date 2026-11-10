namespace Aw.Sindri.Core.Toolkits;

/// <summary>Where a toolkit stands: from its folder on disk to a running process.</summary>
public enum ToolkitState
{
    /// <summary>Not built, or its source changed since the last good build; it can't start.</summary>
    Off = 1,

    /// <summary>Built (zero warnings, tests passed) for the recorded source hash, and not running.</summary>
    Built = 2,

    /// <summary>The process is up and its tools are served.</summary>
    Running = 3,

    /// <summary>The process is up and calls are refused with SND-TK-005.</summary>
    Paused = 4,

    /// <summary>The last build failed, or the process crashed three times in ten minutes.</summary>
    Failed = 5,
}
