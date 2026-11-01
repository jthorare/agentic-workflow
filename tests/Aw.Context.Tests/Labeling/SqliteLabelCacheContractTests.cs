using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Testing.Extract;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "integration")]
public sealed class SqliteLabelCacheContractTests : LabelCacheContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ILabelCache CreateCache() => new SqliteLabelCache(_home.Database, _home.Clock);
}
