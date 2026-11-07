using Aw.Kernel.Data;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>Doctor's check of the human token file (setup-once spec §4, API spec §6): the file is the v0 trust boundary, so it warns when its group or everyone can reach it. No file (no serve has run) is no row.</summary>
/// <param name="settings">Names the token file.</param>
internal sealed class TokenFileDoctorChecks(SindriSettings settings) : IDoctorChecks
{
    private const UnixFileMode GroupOrOther = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(settings.TokenPath))
        {
            return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
        }

        if (FileModes.TryRead(settings.TokenPath) is not { } mode)
        {
            return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([new DoctorCheck("setup:token", CheckState.Warn,
                $"{settings.TokenPath} can't be read (a broken link, or no permission to look at it)",
                $"rm {settings.TokenPath}; the next sindri serve writes a new one")]);
        }

        var reachable = mode & GroupOrOther;
        var check = reachable == UnixFileMode.None
            ? new DoctorCheck("setup:token", CheckState.Ok, $"{settings.TokenPath} can be read by you alone", Fix: null)
            : new DoctorCheck("setup:token", CheckState.Warn,
                $"{settings.TokenPath} can be reached by your group or by everyone, and anything that reads it can confirm for you in the app", $"chmod 600 {settings.TokenPath}");
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([check]);
    }
}
