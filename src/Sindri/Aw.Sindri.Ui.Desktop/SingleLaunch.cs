using System.Diagnostics;
using System.Globalization;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// One app per user (UI spec §15): the first launch claims the instance and runs the app; a later one hands its message
/// ("focus", or the deep link it was started with) to the running instance and exits. The running instance only
/// navigates on it. It is the head's boundary: anything that throws while composing the platform, before or while the
/// app runs, or while ending the instance and the motion poll after it (an unusable or removed 'ui' directory, a socket
/// it can't use) ends the launch with SND-UI-004 and exit 2, logging the code and the exception's type only.
/// </summary>
/// <param name="holderWait">How long a launch whose message got no answer waits for the holder to go before it names it.</param>
internal sealed class SingleLaunch(TimeSpan holderWait)
{
    /// <summary>How long a launch waits for a holder that didn't answer to go (a quitting app settles for up to a second).</summary>
    internal static readonly TimeSpan DefaultHolderWait = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan _claimEvery = TimeSpan.FromMilliseconds(50);
    private const string Scheme = "sindri://";

    // A failure inside the app (at start-up or while it ran): the app's log names the cause, which AW_LOG_LEVEL=none hides.
    private const string FailedInApp = "sindri-ui: failed (SND-UI-004); the log names the cause unless AW_LOG_LEVEL is none";

    /// <summary>Runs the app, or hands over to the running one.</summary>
    /// <param name="instance">This launch's instance port.</param>
    /// <param name="args">The command line.</param>
    /// <param name="runApp">Runs the app until it quits; returns its exit code.</param>
    /// <param name="errors">Standard error.</param>
    /// <param name="log">The head's log, for a failure before the app starts (SND-UI-004).</param>
    /// <returns>The exit code: the app's (2 when it failed), 0 when the running instance handled the message, 1 when it holds the claim but doesn't answer.</returns>
    internal static int Run(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp, TextWriter errors, ILogger log) =>
        new SingleLaunch(DefaultHolderWait).Execute(instance, args, runApp, errors, log);

    /// <summary>As <see cref="Run(ISingleInstance, IReadOnlyList{string}, Func{int}, TextWriter, ILogger)"/>, with this launch's wait for a holder that doesn't answer.</summary>
    /// <param name="instance">This launch's instance port.</param>
    /// <param name="args">The command line.</param>
    /// <param name="runApp">Runs the app until it quits; returns its exit code.</param>
    /// <param name="errors">Standard error.</param>
    /// <param name="log">The head's log.</param>
    /// <returns>The exit code, as Run's.</returns>
    internal int Execute(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp, TextWriter errors, ILogger log)
    {
        try
        {
            try
            {
                return Launch(instance, args, runApp, errors);
            }
            finally
            {
                // Ending the instance (removing its socket and pid files) is inside the boundary too: ui/ may be gone.
                // This runs after runApp returned, so Avalonia's synchronization context is installed: it is safe only because
                // SocketSingleInstance.DisposeAsync uses ConfigureAwait(false) throughout. Removing one there would deadlock on exit.
#pragma warning disable RS0030 // safe to block: SocketSingleInstance.DisposeAsync awaits with ConfigureAwait(false) throughout, so it never needs this thread's context
                instance.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
            }
        }
        catch (Exception e)
        {
            return Failed(e, errors, log);
        }
    }

    /// <summary>Composes the platform, then runs the app over it or hands over to the running one.</summary>
    /// <param name="compose">Builds the head's adapters.</param>
    /// <param name="args">The command line.</param>
    /// <param name="runApp">Runs the app over the platform until it quits; returns its exit code.</param>
    /// <param name="errors">Standard error.</param>
    /// <param name="log">The head's log.</param>
    /// <returns>The exit code, as the other overload's.</returns>
    internal static int Run(Func<SindriPlatform> compose, IReadOnlyList<string> args, Func<SindriPlatform, int> runApp, TextWriter errors, ILogger log)
    {
        SindriPlatform platform;
        try
        {
            platform = compose();
        }
        catch (Exception e)
        {
            return Failed(e, errors, log);
        }

        var exit = Run(platform.Instance, args, () => runApp(platform), errors, log);
        try
        {
            // Ending the motion poll is inside the boundary too (review run 4, pattern-recognition-specialist-4).
            (platform.Motion as IDisposable)?.Dispose();
        }
        catch (Exception e)
        {
            return Failed(e, errors, log);
        }

        return exit;
    }

    // SND-UI-004: the message and stack can carry the home's path, so the code and the type only. The line names the
    // cause itself (the type only), so it holds when AW_LOG_LEVEL=none printed no log line.
    private static int Failed(Exception e, TextWriter errors, ILogger log)
    {
        DesktopLog.Failed(log, e.GetType().Name);
        errors.WriteLine($"sindri-ui: failed (SND-UI-004: {e.GetType().Name})");
        return SindriApp.FailedExitCode;
    }

    private int Launch(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp, TextWriter errors)
    {
        // Before the UI starts, on the main thread with no synchronization context: blocking here deadlocks nothing.
        if (Claim(instance))
        {
            return RunApp(runApp, errors);
        }

#pragma warning disable RS0030 // the launcher is synchronous on the main thread before the UI starts (see the comment above)
        if (instance.Send(MessageFor(args), CancellationToken.None).AsTask().GetAwaiter().GetResult())
        {
#pragma warning restore RS0030
            return 0;
        }

        // No answer: the holder may have just ended (then this launch claims and runs), or be quitting (it refuses at once
        // and goes within a second or so), or it holds the claim without handling messages (a failed start-up, a wedged
        // app), which only the user can end. So claim until the holder goes, for a bounded time (review run 5,
        // julik-frontend-races-reviewer-0).
        var waited = Stopwatch.StartNew();
        do
        {
            if (Claim(instance))
            {
                return RunApp(runApp, errors);
            }

#pragma warning disable RS0030 // the launcher is synchronous on the main thread before the UI starts: it waits out a holder that is still starting
            Thread.Sleep(_claimEvery);
#pragma warning restore RS0030
        }
        while (waited.Elapsed < holderWait);

        var pid = instance.HolderProcessId() is { } id ? id.ToString(CultureInfo.InvariantCulture) : "unknown";
        errors.WriteLine($"sindri-ui: Sindri app is running but not answering (pid {pid}): quit it or kill it");
        return 1;
    }

    /// <summary>What a later launch sends: the deep link it was started with ('sindri://' optional), else "focus".</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The message.</returns>
    internal static string MessageFor(IReadOnlyList<string> args)
    {
        if (args is not [var only])
        {
            return InstanceMessages.Focus;
        }

        var link = only.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? only[Scheme.Length..] : only;
        return InstanceMessages.IsLink(link) && link.Length <= InstanceMessages.MaxLength ? link : InstanceMessages.Focus;
    }

#pragma warning disable RS0030 // the launcher is synchronous on the main thread before the UI starts
    private static bool Claim(ISingleInstance instance) => instance.TryClaim(CancellationToken.None).AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030

    private static int RunApp(Func<int> runApp, TextWriter errors)
    {
        var exit = runApp();
        if (exit == SindriApp.FailedExitCode)
        {
            // The app logged the cause itself; the line stays true when AW_LOG_LEVEL=none printed nothing.
            errors.WriteLine(FailedInApp);
        }

        return exit;
    }
}
