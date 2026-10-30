using Aw.Context.Core.Surfaces;
using Aw.Context.Infrastructure.Surfaces;
using Aw.Context.Testing.Surfaces;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "integration")]
public sealed class SqliteSurfacePickStoreContractTests : SurfacePickStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ISurfacePickStore CreateStore() => new SqliteSurfacePickStore(_home.Database);
}
