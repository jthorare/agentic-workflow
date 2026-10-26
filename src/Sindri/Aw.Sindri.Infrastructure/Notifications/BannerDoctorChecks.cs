using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// Doctor's 'setup:banners' line (GitHub #1111): on macOS a desktop banner is shown only through terminal-notifier, because an osascript banner's click
/// opens Script Editor (see <see cref="NotifierCommand"/>). Without it no banner appears, so doctor says so. Elsewhere there is no line.
/// </summary>
/// <param name="isMacOs">Whether this box is a Mac.</param>
/// <param name="terminalNotifier">Finds terminal-notifier on PATH; null when it is not there.</param>
internal sealed class BannerDoctorChecks(bool isMacOs, Func<string?> terminalNotifier) : IDoctorChecks
{
    internal const string Name = "setup:banners";

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if (!isMacOs)
        {
            return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
        }

        var found = terminalNotifier();
        var check = found is { Length: > 0 }
            ? new DoctorCheck(Name, CheckState.Ok, $"desktop banners go through {found}", Fix: null)
            : new DoctorCheck(Name, CheckState.Warn, "desktop banners are off: terminal-notifier isn't installed", "brew install terminal-notifier (or rerun ./setup.sh)");
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([check]);
    }
}
