using Aw.Judge.Core.Processes;

namespace Aw.Judge.Core.Ports;

/// <summary>Runs a program to completion (the agent CLIs, the keychain reader).</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the program and collects both output streams. Past <see cref="ProcessRequest.Timeout"/> the program and
    /// every process it started are stopped and the result is <see cref="ProcessEnd.TimedOut"/>; a program that isn't
    /// installed is <see cref="ProcessEnd.NotStarted"/>, and one the system refuses to start, or asked to run in a
    /// working directory that doesn't exist, is <see cref="ProcessEnd.FailedToStart"/>. None of them throws.
    /// </summary>
    /// <param name="request">What to run.</param>
    /// <param name="cancellationToken">Stops the program (and its children) and throws OperationCanceledException.</param>
    /// <returns>How it ended and what it wrote.</returns>
    public ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken);
}
