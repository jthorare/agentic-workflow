using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Models;

/// <summary>'context.models' read from the approved profile's 'context' section, once per approved hash (model routing spec §4 "Settings"). The old 'context.conceptScan.models' key is folded in by the context reader.</summary>
/// <param name="approved">The approved profile.</param>
internal sealed class ApprovedModels(IApprovedProfile approved) : IApprovedModels
{
    private volatile Cached? _cached;

    public async ValueTask<ModelsSettings> Current(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } loaded)
        {
            return new ModelsSettings();   // nothing approved routes to the defaults, which are today's behavior
        }

        if (_cached is { } hit && hit.Hash == loaded.Hash)
        {
            return hit.Settings;
        }

        var read = SettingsReader.Read(loaded.Profile.Context);
        var settings = read.IsError ? new ModelsSettings() : read.Value.Models;   // an invalid section is never approved; this only covers a hand-edited store
        _cached = new Cached(loaded.Hash, settings);
        return settings;
    }

    private sealed record Cached(ProfileHash Hash, ModelsSettings Settings);
}
