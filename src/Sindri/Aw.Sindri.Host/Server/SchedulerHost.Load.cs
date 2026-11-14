using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Host.Server;

internal sealed partial class SchedulerHost
{
    /// <summary>Logs an approval that can't be read once for the stretch it keeps failing (SND-SCHED-001).</summary>
    private void NoteProblem(Error error)
    {
        if (!string.Equals(_problem, error.Message, StringComparison.Ordinal))
        {
            _problem = error.Message;
            LogRefreshFailed(log, error.Code, error.Message);
        }
    }

    private async Task Load(LoadedProfile? profile, CancellationToken ct)
    {
        _loaded = true;
        _approved = profile;
        _schedules = ServiceSchedule.From(profile?.Profile, inputs.BuiltIns, clock.GetUtcNow(), await LastRuns(ct).ConfigureAwait(false));
        inputs.Live.Publish(_schedules);
        LogSchedule(log, profile?.Hash.Abbreviated ?? "none", _schedules.Count);
    }
}
