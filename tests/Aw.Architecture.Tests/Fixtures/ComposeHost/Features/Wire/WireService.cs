using Aw.Architecture.Tests.Fixtures.ComposeTarget.Features;

namespace Aw.Architecture.Tests.Fixtures.ComposeHost.Features.Wire;

internal static class WireService
{
    public static ICollection<string> Wire(ICollection<string> services) => services.AddThingFeatures();
}
