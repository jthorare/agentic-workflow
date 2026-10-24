using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Infrastructure.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class LoopbackStaticSiteContractTests : StaticSiteContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override Task<(IStaticSite Site, string Folder)> ArrangeAsync() => Task.FromResult<(IStaticSite, string)>((new LoopbackStaticSite(), _root.Path));

    protected override Task<(IStaticSite Site, string Folder)> ArrangeMissingAsync() => Task.FromResult<(IStaticSite, string)>((new LoopbackStaticSite(), _root.Combine("absent")));

    public void Dispose() => _root.Dispose();
}
