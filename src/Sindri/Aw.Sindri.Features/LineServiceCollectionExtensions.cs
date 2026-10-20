using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Features.PlanContext;
using Aw.Sindri.Features.ResolveParents;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the line's slices (design, plan, parent resolution and the plan's context calls).</summary>
internal static class LineServiceCollectionExtensions
{
    /// <summary>Adds the line's services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLineSlices(this IServiceCollection services) => services.AddDesignSlices().AddPlanSlices().AddResolveParentsSlices().AddPlanContextSlices()
        .AddScoped<IValidator<GetInstanceDesignStopRequest>, AcceptAllValidator<GetInstanceDesignStopRequest>>()
        .AddQuery<GetInstanceDesignStopRequest, DesignStopView, GetInstanceDesignStopService>();
}
