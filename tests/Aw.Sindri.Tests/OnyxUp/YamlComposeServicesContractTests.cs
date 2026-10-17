using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class YamlComposeServicesContractTests : ComposeServicesContract
{
    protected override IComposeServices Create(string realCompose) => new YamlComposeServices();
}
