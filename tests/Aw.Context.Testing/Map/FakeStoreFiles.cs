using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>The store's files as a test sets them: none yet unless <see cref="Current"/> is set.</summary>
public sealed class FakeStoreFiles : IStoreFiles
{
    /// <summary>What <see cref="Sizes"/> returns (null: no store file yet).</summary>
    public StoreFileSizes? Current { get; set; }

    /// <inheritdoc/>
    public Result<StoreFileSizes?> Sizes() => Results.Success(Current);
}
