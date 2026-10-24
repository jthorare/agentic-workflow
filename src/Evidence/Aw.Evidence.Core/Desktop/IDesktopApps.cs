using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Starts the repo's desktop app and keeps it running while a script drives it.</summary>
public interface IDesktopApps
{
    /// <summary>Starts the app on a display (the user's when one is set, else a virtual one) without waiting for it to end.</summary>
    /// <param name="launch">The program, its working directory and its extra environment.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running app; disposing it stops the app and everything it started. AW-EVIDENCE-015 when it cannot start or there is no display to give it.</returns>
    public ValueTask<Result<IRunningApp>> StartAsync(AppLaunch launch, CancellationToken cancellationToken);
}
