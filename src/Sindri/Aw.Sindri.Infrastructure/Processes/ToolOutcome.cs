namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>How a tool run ended.</summary>
internal enum ToolOutcome
{
    /// <summary>It exited on its own; the exit code says how.</summary>
    Exited = 1,

    /// <summary>The tool isn't on PATH.</summary>
    Missing = 2,

    /// <summary>It ran past its timeout and was killed.</summary>
    TimedOut = 3,

    /// <summary>The caller cancelled the run and the tool was killed.</summary>
    Cancelled = 4,
}
