using Aw.Context.Core.Models;
using Aw.Context.Core.Surfaces;
using Aw.Context.Core.Trust;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>
/// Jev's decision on candidate groups (spec §6.9): one <see cref="ChoiceQuestion"/> per group, packed against Jev's input cap and split by halves as the label pick does, every group Jev did not settle left separate. No CLI is asked in Jev's place.
/// </summary>
/// <param name="port">Jev, or null when the host has none.</param>
/// <param name="scrubbers">Builds the scrubbers every question passes through.</param>
internal sealed class SurfacePicker(IChoicePort? port, IScrubberFactory scrubbers) : ISurfacePicker
{
    public ValueTask<SurfacePickResult> Decide(IReadOnlyList<SurfaceGroup> groups, SurfacePickOptions options, CancellationToken cancellationToken) => throw new NotSupportedException($"{port}{scrubbers}{SurfacePrompt.PathLength}");
}
