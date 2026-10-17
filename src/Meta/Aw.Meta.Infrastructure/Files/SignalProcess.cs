using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>SIGTERM by pid through the kill program (the same on Linux and macOS).</summary>
internal static class SignalProcess
{
    internal static async ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken)
    {
        if (pid <= 1)
        {
            return MetaErrors.HostUnreadable.With("refusing to signal pid " + pid.ToString(CultureInfo.InvariantCulture));
        }

        var info = new System.Diagnostics.ProcessStartInfo("kill") { RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add("-TERM");
        info.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        using var kill = System.Diagnostics.Process.Start(info);
        if (kill is null)
        {
            return MetaErrors.HostUnreadable.With("kill could not be started");
        }

        await kill.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return kill.ExitCode == 0 ? Results.Ok() : MetaErrors.HostUnreadable.With("no such process");
    }
}
