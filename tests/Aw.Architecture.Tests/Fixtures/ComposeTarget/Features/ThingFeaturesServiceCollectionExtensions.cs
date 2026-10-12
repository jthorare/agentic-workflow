namespace Aw.Architecture.Tests.Fixtures.ComposeTarget.Features;

/// <summary>The target's Features registration: Add…() is all a composing Host may call.</summary>
public static class ThingFeaturesServiceCollectionExtensions
{
    public static ICollection<string> AddThingFeatures(this ICollection<string> services)
    {
        services.Add("features");
        return services;
    }

    public static string Describe() => "not a registration method";

#pragma warning disable CA2211, MA0069 // Fixture: a mutable public static field the Host must not touch (ARCH-COMPOSE-001).
    public static string Label = "a field, not a registration method";
#pragma warning restore CA2211, MA0069
}
