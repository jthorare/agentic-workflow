using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Waits out a control plane that is down for a launched session's report: the agent's 'workflow complete' can run while Sindri restarts, and its result must not be
/// stranded. Only 'not running' (SND-API-001: no endpoint, or a refused or broken connection) is retried, for at most <see cref="Window"/>, pausing 1 s and doubling up to
/// <see cref="Longest"/>; a refusal, an invalid answer or a timeout comes back at once. One line on standard error per wait.
/// </summary>
internal static class ConnectRetry
{
    /// <summary>How long a call keeps trying before it gives the last 'not running' back.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(2);

    /// <summary>The longest pause between two tries.</summary>
    internal static readonly TimeSpan Longest = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan _first = TimeSpan.FromSeconds(1);

    /// <summary>Runs <paramref name="attempt"/> until it answers anything but 'not running', or <see cref="Window"/> is spent.</summary>
    /// <param name="attempt">One try, safe to repeat.</param>
    /// <param name="clock">The time the window and the pauses run on.</param>
    /// <param name="notice">Prints one line on standard error.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <typeparam name="T">The answer.</typeparam>
    /// <returns>The first answer that isn't 'not running', or the last one when the window ends.</returns>
    internal static async Task<Result<T>> Run<T>(Func<Task<Result<T>>> attempt, TimeProvider clock, Func<string, Task> notice, CancellationToken ct)
    {
        var started = clock.GetTimestamp();
        var pause = _first;
        while (true)
        {
            var result = await attempt().ConfigureAwait(false);
            if (!result.IsError || result.Error.Code != SindriErrors.NotRunning.Code)
            {
                return result;
            }

            var left = Window - clock.GetElapsedTime(started);
            if (left <= TimeSpan.Zero)
            {
                return result;
            }

            var wait = pause < left ? pause : left;
            await notice(string.Create(CultureInfo.InvariantCulture, $"{SindriErrors.NotRunning.Code} the control plane isn't answering; retrying in {wait.TotalSeconds:0} s (up to {left.TotalSeconds:0} s more)")).ConfigureAwait(false);
            await Task.Delay(wait, clock, ct).ConfigureAwait(false);
            pause = pause * 2 < Longest ? pause * 2 : Longest;
        }
    }
}
