#pragma warning disable MA0182, MA0041, CA1822 // red commit: the stub is registered in the commit that implements it
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Models;

/// <summary>'context.models' read from the approved profile's 'context' section, once per approved hash (model routing spec §4 "Settings").</summary>
internal sealed class ApprovedModels : IApprovedModels
{
    /// <summary>Creates the reader.</summary>
    /// <param name="approved">The approved profile.</param>
    public ApprovedModels(IApprovedProfile approved)
    {
        ArgumentNullException.ThrowIfNull(approved);
    }

    public ValueTask<ModelsSettings> Current(CancellationToken cancellationToken) => throw new NotSupportedException();
}
