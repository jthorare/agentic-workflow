using Aw.Context.Core;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadLibrary;

/// <summary>The library store of a host that wired none: every call says the store can't answer (SND-CONTEXT-004). Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnconfiguredLibraryStore : ILibraryStore
{
    public ValueTask<Result<IReadOnlyList<LibraryDoc>>> Docs(RecordAudience mask, string? slug, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LibraryDoc>>());

    public ValueTask<Result<LibraryDoc?>> Doc(RecordKey key, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LibraryDoc?>());

    public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey doc, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LibraryItem>>());

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOfMany(IReadOnlyList<RecordKey> prs, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LibraryClaimRow>>());

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOf(RecordKey pr, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LibraryClaimRow>>());

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsNaming(string slug, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LibraryClaimRow>>());

    private static Result<T> Unwired<T>() =>
        ContextErrors.StoreUnavailable.With("no library store is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
