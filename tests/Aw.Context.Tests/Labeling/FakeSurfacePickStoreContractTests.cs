using Aw.Context.Core.Surfaces;
using Aw.Context.Testing.Surfaces;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class FakeSurfacePickStoreContractTests : SurfacePickStoreContract
{
    protected override ISurfacePickStore CreateStore() => new FakeSurfacePickStore();
}
