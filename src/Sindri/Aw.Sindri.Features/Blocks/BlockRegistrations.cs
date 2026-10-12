using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Features.Blocks.Step;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The blocks and events this build ships (spec §12): pure blocks with AddGate/AddStep/…, the desk Steps and the P6
/// and P7 blocks as descriptors only (AddBlockDescriptor), and the events with AddEvent.
/// </summary>
internal static class BlockRegistrations
{
    /// <summary>Registers the shipped blocks and events.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecipeBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(DeskSteps.Produce)
        .AddBlockDescriptor(DeskSteps.Checks)
        .AddBlockDescriptor(DeskSteps.Verify)
        .AddGate<BudgetAvailableGate>()
        .AddBlockDescriptor(PendingRuntimes.RouteStartable)
        .AddBlockDescriptor(PendingRuntimes.ClaimRef)
        .AddBlockDescriptor(PendingRuntimes.NotifyDesk)
        .AddEvent(RecipeEvents.ItemRouted)
        .AddEvent(RecipeEvents.ClaimCreated)
        .AddEvent(RecipeEvents.NotifyDelivered);
}
