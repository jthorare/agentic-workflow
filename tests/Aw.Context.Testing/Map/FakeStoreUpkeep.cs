using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>An upkeep pass as a test sets it: no store yet unless <see cref="Current"/> is set.</summary>
public sealed class FakeStoreUpkeep : IStoreUpkeep
{
    /// <summary>What <see cref="Run"/> returns (null: no store file yet).</summary>
    public StoreUpkeep? Current { get; set; }

    /// <inheritdoc/>
    public Result<StoreUpkeep?> Run() => Results.Success(Current);
}
