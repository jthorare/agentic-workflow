using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// Registers blocks and events (spec §2, W1): generically, with no assembly scanning, so registration is AOT-safe.
/// Each registers the descriptor (which <see cref="BlockCatalog"/> reads) and the block class, scoped, as itself and
/// as its kind interface. The catalog is a singleton (AddSindriCore).
/// </summary>
public static class BlockServiceCollectionExtensions
{
    /// <summary>Registers the gate <typeparamref name="T"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="T">The gate class.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddGate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
        where T : class, IGate, IDescribedBlock
        => services.AddSingleton(T.Descriptor).AddScoped<T>().AddScoped<IGate, T>();

    /// <summary>Registers the Step <typeparamref name="T"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="T">The Step class.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
        where T : class, IStep, IDescribedBlock
        => services.AddSingleton(T.Descriptor).AddScoped<T>().AddScoped<IStep, T>();

    /// <summary>Registers the effect <typeparamref name="T"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="T">The effect class.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEffect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
        where T : class, IEffectKind, IDescribedBlock
        => services.AddSingleton(T.Descriptor).AddScoped<T>().AddScoped<IEffectKind, T>();

    /// <summary>Registers the evidence provider <typeparamref name="T"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="T">The provider class.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidence<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
        where T : class, IEvidenceProvider, IDescribedBlock
        => services.AddSingleton(T.Descriptor).AddScoped<T>().AddScoped<IEvidenceProvider, T>();

    /// <summary>Registers the Step input <typeparamref name="T"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="T">The Step input class.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddStepInput<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
        where T : class, IStepInput, IDescribedBlock
        => services.AddSingleton(T.Descriptor).AddScoped<T>().AddScoped<IStepInput, T>();

    /// <summary>
    /// Registers a block's descriptor without a runtime (spec §12: the P6 and P7 blocks ship in P1 this way), so
    /// recipes that name it validate; at run time it is unavailable (W5).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="descriptor">The block's descriptor.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBlockDescriptor(this IServiceCollection services, BlockDescriptor descriptor)
        => services.AddSingleton(descriptor);

    /// <summary>Registers an event a wait state can wait for.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="descriptor">The event's descriptor.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvent(this IServiceCollection services, EventDescriptor descriptor)
        => services.AddSingleton(descriptor);
}
