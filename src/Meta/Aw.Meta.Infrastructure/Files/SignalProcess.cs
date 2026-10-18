using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>SIGTERM by pid through the kill program (on Linux here; macOS goes through its host reader).</summary>
internal static class SignalProcess
{
    /// <summary>The error for a pid that must never be signalled (init, or not a process), else null.</summary>
    /// <param name="pid">The process id.</param>
    /// <returns>AW-META-010, or null.</returns>
    internal static Error? Refusal(int pid) =>
        pid <= 1 ? MetaErrors.HostUnreadable.With("refusing to signal pid " + pid.ToString(CultureInfo.InvariantCulture)) : null;

    internal static async ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken)
    {
        if (Refusal(pid) is { } refusal)
        {
            return refusal;
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
