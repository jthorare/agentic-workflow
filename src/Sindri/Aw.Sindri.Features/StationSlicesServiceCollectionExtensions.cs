using Aw.Sindri.Features.BugFix;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Features.Review;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Features.Ship;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the line's stations: scoping, design and plan, build, ship, the lens loops, prove, and the evidence and docs blocks.</summary>
internal static class StationSlicesServiceCollectionExtensions
{
    /// <summary>Adds every station's block runtimes and slices.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStationSlices(this IServiceCollection services) =>
        services.AddScopingSlices().AddLineSlices().AddLineBlockRuntimes().AddBuildSlices().AddShipSlices().AddLensLoops().AddProveSlices().AddEvidenceSlices().AddDocsSlices().AddReviewSlices().AddBugFixSlices().AddPrBlocks();
}
