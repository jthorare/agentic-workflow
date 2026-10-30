using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Whether a Jev pick call can go out (model routing spec §6.7): the pick setting of the APPROVED profile, read at every call (never the live file), and the Jev key. <see cref="JevChoicePort"/> asks it before each call and answers
/// <see cref="IChoicePort.Unavailable"/> from it.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="key">Whether a Jev key is available (the same probe the status line uses).</param>
internal sealed class JevAvailability(IApprovedModels models, IJevKeyProbe key)
{
    // The approved profile, read at every call: an edited live file that says 'jev' makes no Jev call until it is approved. Each kind has its own key: 'labeling.pick: jev' alone sends no surface data (spec §6.9, q154).
    internal async ValueTask<bool> IsOn(ChoiceKind kind, CancellationToken cancellationToken)
    {
        var settings = await models.Current(cancellationToken).ConfigureAwait(false);
        return (kind == ChoiceKind.Labeling ? settings.LabelPick : settings.SurfacePick) == LabelPick.Jev;
    }

    // A cheap read: the approved setting first (an 'off' pick says off whether or not a key exists), then the key.
    internal async ValueTask<string?> Unavailable(ChoiceKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsOn(kind, cancellationToken).ConfigureAwait(false))
        {
            return ChoiceReasons.Off;
        }

        return await key.Present(cancellationToken).ConfigureAwait(false) ? null : ChoiceReasons.NoKey;
    }
}
