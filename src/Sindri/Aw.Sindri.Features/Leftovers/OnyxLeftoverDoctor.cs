using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.Leftovers;

/// <summary>
/// Doctor's `index:onyx-leftover` row (owned index spec §5.8): what an old `sindri onyx up` left on this box (containers and volumes under its compose label, its keychain entry, the onyx/ directory),
/// read-only through <see cref="OnyxLeftovers"/>. Anything left fails, and the fix is `sindri onyx down --purge`; once the approved profile sets `context.index.acknowledgeOnyxLeftover`
/// it warns instead. Nothing left is no row. Docker answering wrongly is a warning, never a failure.
/// </summary>
/// <param name="approvals">The approved profile (for the acknowledge flag).</param>
/// <param name="engine">Reads the profile's context settings.</param>
/// <param name="home">The Sindri home the leftovers are looked up under.</param>
/// <param name="docker">Lists containers and volumes by compose label.</param>
/// <param name="files">Reads state.json and finds onyx/.</param>
internal sealed class OnyxLeftoverDoctor(IApprovedProfile approvals, IContextEngine engine, ISindriHome home, IPurgeDocker docker, ILeftoverFiles files) : IDoctorChecks
{
    /// <summary>The check's name.</summary>
    internal const string Name = "index:onyx-leftover";

    private const string Fix = "sindri onyx down --purge";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var found = OnyxLeftovers.Find(home.Path, docker, files);
        if (found.IsError)
        {
            return [new DoctorCheck(Name, CheckState.Warn, $"not checked: {found.Error.Message}", Fix)];
        }

        if (found.Value.IsEmpty)
        {
            return [];
        }

        var detail = "old Onyx left " + string.Join(", ", Parts(found.Value));
        return await Acknowledged(cancellationToken).ConfigureAwait(false)
            ? [new DoctorCheck(Name, CheckState.Warn, $"{detail} (acknowledged in context.index.acknowledgeOnyxLeftover)", Fix)]
            : [new DoctorCheck(Name, CheckState.Fail, detail, Fix)];
    }

    private static IEnumerable<string> Parts(PurgePlan plan)
    {
        if (plan.Containers.Count > 0)
        {
            yield return $"{plan.Containers.Count} container(s)";
        }

        if (plan.Volumes.Count > 0)
        {
            yield return $"{plan.Volumes.Count} volume(s)";
        }

        if (plan.Directory is not null)
        {
            yield return $"the {plan.Directory} directory";
        }

        if (plan.Keychain is not null)
        {
            yield return $"the keychain entry {plan.Keychain}";
        }
    }

    private async ValueTask<bool> Acknowledged(CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } loaded)
        {
            return false;
        }

        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        return settings.IsSuccess && settings.Value.Index.AcknowledgeOnyxLeftover;
    }
}
