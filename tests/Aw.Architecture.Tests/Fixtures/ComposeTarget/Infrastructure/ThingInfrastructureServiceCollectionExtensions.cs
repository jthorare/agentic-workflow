namespace Aw.Architecture.Tests.Fixtures.ComposeTarget.Infrastructure;

/// <summary>The target's Infrastructure registration.</summary>
public static class ThingInfrastructureServiceCollectionExtensions
{
    public static ICollection<string> AddThingInfrastructure(this ICollection<string> services)
    {
        services.Add("infrastructure");
        return services;
    }
}
