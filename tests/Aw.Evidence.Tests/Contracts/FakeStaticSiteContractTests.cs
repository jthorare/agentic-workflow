using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeStaticSiteContractTests : StaticSiteContract
{
    private const string Folder = "/work/repo/docs/mockups";

    protected override Task<(IStaticSite Site, string Folder)> ArrangeAsync() => Task.FromResult<(IStaticSite, string)>((new FakeStaticSite(), Folder));

    protected override Task<(IStaticSite Site, string Folder)> ArrangeMissingAsync()
    {
        var fake = new FakeStaticSite();
        _ = fake.Missing.Add(Folder);
        return Task.FromResult<(IStaticSite, string)>((fake, Folder));
    }
}
