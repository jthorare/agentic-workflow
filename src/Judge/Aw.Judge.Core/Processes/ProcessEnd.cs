namespace Aw.Judge.Core.Processes;

/// <summary>How a program run ended.</summary>
public enum ProcessEnd
{
    /// <summary>The program exited by itself; its exit code is set.</summary>
    Exited = 1,

    /// <summary>The program ran past its timeout and was stopped with every process it started.</summary>
    TimedOut = 2,

    /// <summary>The program isn't installed: not found on the search path, or not executable.</summary>
    NotStarted = 3,

    /// <summary>
    /// The program is there but the system refused to start it (an argument list past the system's limit, a file that
    /// isn't a program, no permission): a real failure, unlike <see cref="NotStarted"/>.
    /// </summary>
    FailedToStart = 4,
}
