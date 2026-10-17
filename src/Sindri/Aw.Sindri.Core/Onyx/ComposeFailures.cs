using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The SND-ONYX-011 a compose command that ran past its time limit stands for, and the way to tell it from another compose failure (a step may wait it out).</summary>
public static class ComposeFailures
{
    private const string Marker = "didn't finish within";

    /// <summary>Builds the error of a docker command that was stopped for running too long.</summary>
    /// <param name="what">The command as it reads in the message, such as compose up.</param>
    /// <param name="timeout">The limit it ran past.</param>
    /// <returns>SND-ONYX-011 saying how long it was allowed; the command that ran it adds what to run next (<see cref="WithHint"/>).</returns>
    public static Error TimedOut(string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(what);
        var allowed = timeout.TotalMinutes >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{timeout.TotalMinutes:0.#} minutes")
            : string.Create(CultureInfo.InvariantCulture, $"{timeout.TotalSeconds:0} seconds");
        return SindriErrors.OnyxComposeFailed.With($"`docker {what}` {Marker} {allowed} and was stopped");
    }

    /// <summary>Builds the error of a `docker compose stop` that was stopped for running too long: the containers may be half stopped, and a rerun finishes the job.</summary>
    /// <param name="timeout">The limit it ran past.</param>
    /// <returns>SND-ONYX-011 saying the stop was cut short.</returns>
    public static Error StopCutShort(TimeSpan timeout)
        => SindriErrors.OnyxComposeFailed.With(string.Create(CultureInfo.InvariantCulture, $"the stop was cut short: `docker compose stop` {Marker} {timeout.TotalMinutes:0.#} minutes and was stopped, so some containers may still be running; run `sindri onyx down` again"));

    /// <summary>Adds what to run next to an SND-ONYX-011, as the command that ran the docker call knows it (up resumes, down runs again, status and doctor repair the files), so no command is told another one's fix.</summary>
    /// <param name="error">Any error.</param>
    /// <param name="hint">The next step, such as run `sindri onyx down` again.</param>
    /// <returns>The error with the hint after its message; any other error, or one that already names a `sindri onyx` command, unchanged.</returns>
    public static Error WithHint(Error error, string hint)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(hint);
        return error.Code == SindriErrors.OnyxComposeFailed.Code && !error.Message.Contains("`sindri onyx ", StringComparison.Ordinal)
            ? error with { Message = error.Message + "; " + hint }
            : error;
    }

    /// <summary>Tells whether an error is a docker command that was stopped for running too long.</summary>
    /// <param name="error">Any error.</param>
    /// <returns>True for the error <see cref="TimedOut"/> builds.</returns>
    public static bool IsTimeout(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code == SindriErrors.OnyxComposeFailed.Code && error.Message.Contains(Marker, StringComparison.Ordinal);
    }
}
