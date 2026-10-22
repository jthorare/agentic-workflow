using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The block catalogs the recipe tests validate against: the shipped one, and the shipped one plus the test-only fixture blocks.</summary>
public static class FixtureCatalog
{
    /// <summary>The blocks and events this build ships (BlockRegistrations.AddRecipeBlocks).</summary>
    /// <returns>The catalog.</returns>
    public static BlockCatalog Shipped() => From(ShippedServices());

    /// <summary>The recipe blocks plus the context Step inputs, whose runtimes the Features composition registers (S-LB2).</summary>
    private static ServiceCollection ShippedServices()
    {
        var services = new ServiceCollection();
        _ = services.AddRecipeBlocks()
            .AddBlockDescriptor(ContextLensesInput.Descriptor)
            .AddBlockDescriptor(ContextHistoryInput.Descriptor)
            .AddBlockDescriptor(ContextCodeInput.Descriptor)
            .AddBlockDescriptor(ContextRetrieveInput.Descriptor);
        return services;
    }

    /// <summary>The shipped catalog plus <see cref="FixtureBlocks"/>, for the validate-only fixtures under Recipes/valid.</summary>
    /// <returns>The catalog.</returns>
    public static BlockCatalog WithFixtures()
    {
        var services = ShippedServices();
        foreach (var block in FixtureBlocks.Bugfix.Concat(FixtureBlocks.Direction))
        {
            _ = services.AddBlockDescriptor(block);
        }

        foreach (var e in FixtureBlocks.DirectionEvents)
        {
            _ = services.AddEvent(e);
        }

        return From(services);
    }

    // why: AddSingleton(instance) registers each descriptor as its instance, so the catalog reads them without a container
    private static BlockCatalog From(IServiceCollection services) => new(
        services.Where(d => d.ServiceType == typeof(BlockDescriptor)).Select(d => (BlockDescriptor)d.ImplementationInstance!),
        services.Where(d => d.ServiceType == typeof(EventDescriptor)).Select(d => (EventDescriptor)d.ImplementationInstance!));
}
