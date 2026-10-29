using Aw.Context.Core.Engine;

namespace Aw.Sindri.Host.Models;

/// <summary>'context.models' of the APPROVED profile (model routing spec §4): never the live file, so an edit nobody approved routes nothing.</summary>
internal interface IApprovedModels
{
    /// <summary>The settings of the latest approved profile; the defaults when none is approved or its 'context' section doesn't read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The settings.</returns>
    public ValueTask<ModelsSettings> Current(CancellationToken cancellationToken);
}
