using Aw.Architecture.Tests.Fixtures.ComposeTarget.Features;
using Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.Make;

namespace Aw.Architecture.Tests.Fixtures.ComposeHost.Host.Commands;

internal static class ShowCommand
{
    public static string Run() => ThingFeaturesServiceCollectionExtensions.Describe() + new MakeRequest("x").Name;
}
