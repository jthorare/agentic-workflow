using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>
/// Drives one screen at one window size: starts the app on its own display with the UI driver on and a fresh driver directory, waits for the driver,
/// sets the window size, runs the screen's script through the driver's command line and stops the app again, even after a failure. A step whose
/// element is missing or whose wait runs out ends the script as that screen's problem; a driver that cannot be reached, or an app that will not
/// start, is the run's error.
/// </summary>
internal sealed class DesktopDriving(IDesktopApps apps, IDesktopCommands commands, IScratchDirectories scratch, IRunFiles files)
{
    private const string DriverPrefix = "aw-driver-";
    private const int CheckFailed = 2;

    internal async ValueTask<Result<DrivenScreen>> DriveAsync(DesktopPlan plan, DesktopScreen screen, ViewportSize size, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var frame = plan.FilesOf(screen, size).App;
        var made = await files.CreateDirectoryAsync(frame[..frame.LastIndexOf('/')], cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var directory = scratch.Create(DriverPrefix);
        if (directory.IsError)
        {
            return directory.Error;
        }

        try
        {
            var driver = new Dictionary<string, string>(environment, StringComparer.Ordinal) { ["AW_UI_DRIVER"] = "1", ["AW_UI_DRIVER_DIR"] = directory.Value };
            return await StartAndDriveAsync(new Run(plan, screen, size, directory.Value, driver, frame), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scratch.Remove(directory.Value);
        }
    }

    private async ValueTask<Result<DrivenScreen>> StartAndDriveAsync(Run run, CancellationToken cancellationToken)
    {
        var what = $"{run.Screen.Name} at {run.Size.Describe()}";
        var started = await apps.StartAsync(new AppLaunch(run.Plan.Settings.App.Command, run.Plan.Repo, run.Environment), cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return EvidenceErrors.RunFailed.With($"launch {what}: {started.Error.Message}");
        }

        await using (started.Value.ConfigureAwait(false))
        {
            var prepared = await PrepareAsync(run, started.Value, what, cancellationToken).ConfigureAwait(false);
            if (prepared.IsError)
            {
                return prepared.Error;
            }

            var problem = await RunScriptAsync(run, cancellationToken).ConfigureAwait(false);
            return problem.IsError ? problem.Error : new DrivenScreen(started.Value.Display, problem.Value);
        }
    }

    // The driver must answer and the window must take its size; neither can be the screen's fault, so any failure is the run's error.
    private async ValueTask<Result<Unit>> PrepareAsync(Run run, IRunningApp app, string what, CancellationToken cancellationToken)
    {
        var ready = await DriveStepAsync(run, DriverCommandLine.Ready(run.Plan.Settings.DriverCommand, run.Directory), "driver ready", cancellationToken).ConfigureAwait(false);
        if (Failure(ready) is { } notReady)
        {
            return EvidenceErrors.RunFailed.With($"launch {what}: the app's driver did not answer: {notReady}{AppEnd(app)}");
        }

        var window = await DriveStepAsync(run, DriverCommandLine.Window(run.Plan.Settings.DriverCommand, run.Directory, run.Size), "window", cancellationToken).ConfigureAwait(false);
        return Failure(window) is { } notSized ? EvidenceErrors.RunFailed.With($"window {what}: {notSized}{AppEnd(app)}") : Unit.Value;
    }

    private static string? Failure(Result<string?> outcome) => outcome.IsError ? outcome.Error.Message : outcome.Value;

    // Each step's commands in order; the first check that fails ends the script as the screen's problem.
    private async ValueTask<Result<string?>> RunScriptAsync(Run run, CancellationToken cancellationToken)
    {
        var steps = run.Screen.Steps;
        for (var i = 0; i < steps.Count; i++)
        {
            foreach (var line in DriverCommandLine.Step(run.Plan.Settings.DriverCommand, run.Directory, steps[i], run.Frame))
            {
                var label = string.Create(CultureInfo.InvariantCulture, $"step {i + 1} ({Describe(steps[i])})");
                var outcome = await DriveStepAsync(run, line, label, cancellationToken).ConfigureAwait(false);
                if (outcome.IsError || outcome.Value is not null)
                {
                    return outcome;
                }
            }
        }

        return Results.Success<string?>(value: null);
    }

    // Exit 0 is done (null); the driver's 'a check failed' exit is the screen's problem; any other exit is an error of the environment.
    private async ValueTask<Result<string?>> DriveStepAsync(Run run, IReadOnlyList<string> line, string label, CancellationToken cancellationToken)
    {
        var ran = await commands.RunAsync(new CommandSpec(line, run.Plan.Repo, run.Environment, run.Plan.Settings.Timeout), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return EvidenceErrors.RunFailed.With($"{label}: {ran.Error.Message}");
        }

        return ran.Value.ExitCode switch
        {
            0 => Results.Success<string?>(value: null),
            CheckFailed => Results.Success<string?>($"{label} failed: {CommandTail.Of(ran.Value.StandardError)}"),
            _ => EvidenceErrors.RunFailed.With(string.Create(CultureInfo.InvariantCulture, $"{label} exited {ran.Value.ExitCode}: {CommandTail.Of(ran.Value.StandardError)}")),
        };
    }

    private static string AppEnd(IRunningApp app) => app.Ended() is { } ended ? $" (the app {ended})" : string.Empty;

    private static string Describe(DesktopStep step) => step.Action switch
    {
        DesktopAction.Launch => "launch",
        DesktopAction.Click => "click",
        DesktopAction.Type => "type",
        DesktopAction.Key => "key",
        DesktopAction.WaitFor => "wait-for",
        DesktopAction.Screenshot => "screenshot",
        _ => "step",
    };

    private sealed record Run(DesktopPlan Plan, DesktopScreen Screen, ViewportSize Size, string Directory, Dictionary<string, string> Environment, string Frame);
}
