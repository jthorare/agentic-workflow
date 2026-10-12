using Aw.Architecture.Tests.Fixtures.ComposeTarget.Features;
using Aw.Architecture.Tests.Fixtures.ComposeTarget.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.ComposeHost.Host;

internal static class Composition
{
    public static ICollection<string> Compose(ICollection<string> services) =>
        services.AddThingFeatures().AddThingInfrastructure();

    public static string Labelled(ICollection<string> services) =>
        services.AddThingFeatures().First() + ThingFeaturesServiceCollectionExtensions.Label;
}
