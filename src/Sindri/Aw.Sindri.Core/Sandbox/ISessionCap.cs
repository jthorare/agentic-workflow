using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The atomic concurrent-session cap (sandbox spec §3.3 "Concurrent sessions", §3.9): counting the live scopes and starting a session are one step
/// under one lock, so two launches cannot both see a free slot.
/// </summary>
/// <remarks>
/// When the cap is full the result is SND-LAUNCH-020 (<see cref="SindriErrors.LaunchSessionWait"/>). An attended open treats it as refused at once;
/// the launch activity retries it. The launcher wiring is SB-5a's.
/// </remarks>
public interface ISessionCap
{
    /// <summary>Takes the lock, counts the live scopes and, when there is room, runs <paramref name="start"/> before releasing the lock.</summary>
    /// <param name="cap">The most sessions that may be alive (the profile's 'sandbox.limits.sessions').</param>
    /// <param name="start">Starts the session; its scope must exist when it returns, or the next launch miscounts.</param>
    /// <param name="cancellationToken">Cancels the wait for the lock.</param>
    /// <typeparam name="T">What starting a session returns.</typeparam>
    /// <returns>Whatever <paramref name="start"/> returned, or SND-LAUNCH-020 when the live count was already at the cap.</returns>
    public Task<Result<T>> Start<T>(int cap, Func<CancellationToken, Task<Result<T>>> start, CancellationToken cancellationToken);
}
