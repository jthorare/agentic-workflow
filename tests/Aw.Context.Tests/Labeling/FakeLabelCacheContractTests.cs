using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class FakeLabelCacheContractTests : LabelCacheContract
{
    protected override ILabelCache CreateCache() => new FakeLabelCache();
}
