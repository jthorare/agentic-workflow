using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>
/// Builds the seeded data with the repo's fixture command in a scratch directory, drives every screen at every size through the app's UI driver
/// (each on its own launch of the app), and tears the data down again, even after a failure. A command that fails ends the run as errored, with the
/// tail of its stderr.
/// </summary>
internal sealed class DesktopStack(IDesktopCommands commands, IScratchDirectories scratch, DesktopDriving driving)
{
    private const string ScratchPrefix = "aw-desktop-";

    internal async ValueTask<Result<DesktopCaptures>> RunAsync(DesktopPlan plan, CancellationToken cancellationToken)
    {
        var home = scratch.Create(ScratchPrefix);
        if (home.IsError)
        {
            return home.Error;
        }

        var environment = new Dictionary<string, string>(plan.Settings.Env, StringComparer.Ordinal) { [plan.Settings.HomeEnv] = home.Value };
        try
        {
            return await UpAndDriveAsync(plan, environment, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DownAsync(plan, environment).ConfigureAwait(false);
            scratch.Remove(home.Value);
        }
    }

    private async ValueTask<Result<DesktopCaptures>> UpAndDriveAsync(DesktopPlan plan, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        foreach (var step in Steps(plan))
        {
            var ran = await RunCommandAsync(plan, [.. plan.Settings.Fixture.Command, .. step], environment, $"fixture {string.Join(' ', step)}", cancellationToken).ConfigureAwait(false);
            if (ran.IsError)
            {
                return ran.Error;
            }
        }

        List<string> notes = [];
        Dictionary<string, string> problems = new(StringComparer.Ordinal);
        foreach (var size in plan.Sizes)
        {
            var driven = await DriveSizeAsync(plan, size, environment, cancellationToken).ConfigureAwait(false);
            if (driven.IsError)
            {
                return driven.Error;
            }

            notes.AddRange(driven.Value.Notes.Where(n => !notes.Contains(n, StringComparer.Ordinal)));
            foreach (var (key, problem) in driven.Value.Problems)
            {
                problems[key] = problem;
            }
        }

        return new DesktopCaptures(notes, problems);
    }

    private async ValueTask<Result<DesktopCaptures>> DriveSizeAsync(DesktopPlan plan, ViewportSize size, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        List<string> notes = [];
        Dictionary<string, string> problems = new(StringComparer.Ordinal);
        foreach (var screen in plan.At(size))
        {
            var driven = await driving.DriveAsync(plan, screen, size, environment, cancellationToken).ConfigureAwait(false);
            if (driven.IsError)
            {
                return driven.Error;
            }

            if (!notes.Contains(driven.Value.Display, StringComparer.Ordinal))
            {
                notes.Add(driven.Value.Display);
            }

            if (driven.Value.Problem is { } problem)
            {
                problems[DesktopPlan.KeyOf(screen, size)] = problem;
            }
        }

        return new DesktopCaptures(notes, problems);
    }

    // The config's steps, then each screen's own, once each and in order of first use.
    private static List<IReadOnlyList<string>> Steps(DesktopPlan plan)
    {
        List<IReadOnlyList<string>> steps = [.. plan.Settings.Fixture.Up];
        foreach (var step in plan.Screens.SelectMany(static s => s.Fixture ?? []))
        {
            if (!steps.Exists(known => known.SequenceEqual(step, StringComparer.Ordinal)))
            {
                steps.Add(step);
            }
        }

        return steps;
    }

    // Teardown never takes the run's cancellation: a cancelled run still stops what it started (its command has its own time limit).
    private async ValueTask DownAsync(DesktopPlan plan, Dictionary<string, string> environment)
    {
        if (plan.Settings.Fixture.Down is { Count: > 0 } down)
        {
            _ = await RunCommandAsync(plan, [.. plan.Settings.Fixture.Command, .. down], environment, "fixture down", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<string>> RunCommandAsync(DesktopPlan plan, IReadOnlyList<string> argv, Dictionary<string, string> environment, string what, CancellationToken cancellationToken)
    {
        var ran = await commands.RunAsync(new CommandSpec(argv, plan.Repo, environment, plan.Settings.Timeout), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return EvidenceErrors.RunFailed.With($"{what}: {ran.Error.Message}");
        }

        return ran.Value.ExitCode == 0
            ? ran.Value.StandardError
            : EvidenceErrors.RunFailed.With(string.Create(CultureInfo.InvariantCulture, $"{what} exited {ran.Value.ExitCode}: {CommandTail.Of(ran.Value.StandardError)}"));
    }
}
