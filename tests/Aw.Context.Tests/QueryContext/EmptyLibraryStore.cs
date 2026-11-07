using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.QueryContext;

/// <summary>A library with no docs and no claims, for the facade tests that never read it.</summary>
internal sealed class EmptyLibraryStore : ILibraryStore
{
    public ValueTask<Result<IReadOnlyList<LibraryDoc>>> Docs(RecordAudience mask, string? slug, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryDoc>>([]));

    public ValueTask<Result<LibraryDoc?>> Doc(RecordKey key, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LibraryDoc?>(value: null));

    public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey doc, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryItem>>([]));

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOfMany(IReadOnlyList<RecordKey> prs, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryClaimRow>>([]));

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOf(RecordKey pr, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryClaimRow>>([]));

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsNaming(string slug, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryClaimRow>>([]));
}
