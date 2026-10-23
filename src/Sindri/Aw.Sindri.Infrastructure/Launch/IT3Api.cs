using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The T3 Code calls the T3 host makes (the adapter's own seam, so the host is tested against a fake T3). Every member returns
/// SND-LAUNCH-025 when T3 can't be reached.
/// </summary>
internal interface IT3Api
{
    /// <summary>The running server's environment.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Its id and version.</returns>
    public ValueTask<Result<T3Environment>> Environment(CancellationToken cancellationToken);

    /// <summary>Every project, with its folder and the repository T3 knows it as.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The projects.</returns>
    public ValueTask<Result<IReadOnlyList<T3Project>>> Projects(CancellationToken cancellationToken);

    /// <summary>The first enabled provider instance of <paramref name="driverKind"/> that has a model, with its first model (T3 lists favourites first), or null.</summary>
    /// <param name="driverKind">The driver ('claudeAgent', 'codex', 'cursor').</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instance and model, or null when T3 has none enabled.</returns>
    public ValueTask<Result<T3Model?>> ModelFor(string driverKind, CancellationToken cancellationToken);

    /// <summary>Starts a thread and its first turn.</summary>
    /// <param name="request">What to start.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The thread id.</returns>
    public ValueTask<Result<string>> Launch(T3LaunchRequest request, CancellationToken cancellationToken);

    /// <summary>A thread's state, or null when T3 has no such thread.</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state, or null.</returns>
    public ValueTask<Result<T3ThreadState?>> Read(string threadId, CancellationToken cancellationToken);

    /// <summary>Stops the thread's running turn; a thread with nothing running, or none at all, is a success.</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or SND-LAUNCH-025.</returns>
    public ValueTask<Result<Unit>> Interrupt(string threadId, CancellationToken cancellationToken);
}
