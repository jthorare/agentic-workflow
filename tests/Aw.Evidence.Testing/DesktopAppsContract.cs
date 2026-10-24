using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IDesktopApps contract: a started app runs until it is disposed and says how it got its display, and a program that would not start is AW-EVIDENCE-015.</summary>
public abstract class DesktopAppsContract
{
    /// <summary>Arranges a program that runs until it is stopped.</summary>
    /// <returns>The starter, the program and its folder.</returns>
    protected abstract Task<DesktopAppsUnderTest> ArrangeAsync();

    /// <summary>Arranges a program that is not there.</summary>
    /// <returns>The starter, the missing program and a folder.</returns>
    protected abstract Task<DesktopAppsUnderTest> ArrangeMissingAsync();

    /// <summary>The app is running after the call returns and has ended once it is disposed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_started_app_runs_until_it_is_disposed()
    {
        var run = await ArrangeAsync();

        var started = await run.Apps.StartAsync(Launch(run), CancellationToken.None);

        Expect.That(started.IsSuccess, "the app starts");
        var app = started.Value;
        Expect.That(app.Ended() is null, "it is running when the call returns");
        Expect.That(app.Display.StartsWith("display: ", StringComparison.Ordinal), "it says how it got its display");
        await app.DisposeAsync();
        Expect.That(app.Ended() is not null, "it has ended after it was disposed");
    }

    /// <summary>A program that is not there is a run failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_program_that_would_not_start_is_a_run_failure()
    {
        var run = await ArrangeMissingAsync();

        var started = await run.Apps.StartAsync(Launch(run), CancellationToken.None);

        Expect.That(started.IsError && started.Error.Code == EvidenceErrors.RunFailed.Code, "a program that cannot start is AW-EVIDENCE-015");
    }

    private static AppLaunch Launch(DesktopAppsUnderTest run) =>
        new([run.Program], run.WorkingDirectory, new Dictionary<string, string>(StringComparer.Ordinal) { ["DISPLAY"] = ":0" });
}
