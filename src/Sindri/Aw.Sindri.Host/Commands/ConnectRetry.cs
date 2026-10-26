using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Waits out a control plane that is down for a launched session's report (#1000, #1109): the agent's 'workflow complete' can run while Sindri restarts, and its result must not be stranded.
/// Only 'not running' (SND-API-001: no endpoint, or a refused or broken connection) is retried, for at most the window (<see cref="Default"/>, enough for a reinstall; AW_SINDRI_COMPLETE_WAIT
/// seconds sets it, 0 turns the wait off), pausing 1 s and doubling up to <see cref="Longest"/>; a refusal, an invalid answer or a timeout comes back at once. One line on standard error per wait.
/// </summary>
internal static class ConnectRetry
{
    /// <summary>The variable that sets the window, in whole seconds (0: no wait).</summary>
    internal const string Variable = "AW_SINDRI_COMPLETE_WAIT";

    /// <summary>How long a call keeps trying by default: a reinstall stops Sindri, publishes and starts it again, which took 11 minutes in the #1000 incident.</summary>
    internal static readonly TimeSpan Default = TimeSpan.FromMinutes(15);

    /// <summary>The longest pause between two tries.</summary>
    internal static readonly TimeSpan Longest = TimeSpan.FromSeconds(15);

    private const int MaxSeconds = 24 * 60 * 60;
    private static readonly TimeSpan _first = TimeSpan.FromSeconds(1);

    /// <summary>The window <paramref name="environment"/> sets.</summary>
    /// <param name="environment">Reads a process variable; null reads none.</param>
    /// <returns>The window; <see cref="Default"/> when the variable is unset or not a whole number of seconds from 0 to a day.</returns>
    internal static TimeSpan WindowOf(Func<string, string?>? environment) =>
        environment?.Invoke(Variable) is { Length: > 0 } text && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds <= MaxSeconds
            ? TimeSpan.FromSeconds(seconds)
            : Default;

    /// <summary>Runs <paramref name="attempt"/> until it answers anything but 'not running', or <paramref name="window"/> is spent.</summary>
    /// <param name="attempt">One try, safe to repeat.</param>
    /// <param name="window">How long to keep trying.</param>
    /// <param name="clock">The time the window and the pauses run on.</param>
    /// <param name="notice">Prints one line on standard error.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <typeparam name="T">The answer.</typeparam>
    /// <returns>The first answer that isn't 'not running', or the last one when the window ends.</returns>
    internal static async Task<Result<T>> Run<T>(Func<Task<Result<T>>> attempt, TimeSpan window, TimeProvider clock, Func<string, Task> notice, CancellationToken ct)
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

            var left = window - clock.GetElapsedTime(started);
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
