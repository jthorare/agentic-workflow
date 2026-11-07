using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Library;

/// <summary>
/// The design-doc library's stored rows (migration 0031, spec 2026-11-04 §5.3), read without the audience rule's last word: a read returns the live rows a superset mask lets through, and the service applies
/// <see cref="Audience.AudienceScope.Allows(AudienceKey?, RecordKey)"/> to each, so the rule lives in one place. Only live records count (<c language="text">removed_at IS NULL</c>).
/// Every method answers an error, never throws, for a store that can't be read ('SND-CONTEXT-004').
/// </summary>
// why: internal so no host can read the library rows without the audience rule; Features and Infrastructure see it through InternalsVisibleTo, and MA0182 only looks inside this assembly
#pragma warning disable MA0182
internal interface ILibraryStore
{
    /// <summary>Lists the live docs, in key order.</summary>
    /// <param name="mask">Which records may be returned at most: a record whose audience is allowed or that is listed (an 'owner:' record only through its audience).</param>
    /// <param name="slug">Only docs with this front-matter id, or null for all.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The docs.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryDoc>>> Docs(RecordAudience mask, string? slug, CancellationToken cancellationToken);

    /// <summary>Reads one live doc, whatever its audience.</summary>
    /// <param name="key">The doc record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The doc, or null when there is none.</returns>
    public ValueTask<Result<LibraryDoc?>> Doc(RecordKey key, CancellationToken cancellationToken);

    /// <summary>Reads a doc's items in document order, whatever the doc's audience.</summary>
    /// <param name="doc">The doc record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The items; none for an unknown or itemless doc.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey doc, CancellationToken cancellationToken);

    /// <summary>Reads the claims of one live PR record, whatever its audience.</summary>
    /// <param name="pr">The PR record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its claims in ref order; none for an unknown or removed PR.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOf(RecordKey pr, CancellationToken cancellationToken);

    /// <summary>Reads the claims of many live PR records in one query, whatever their audience.</summary>
    /// <param name="prs">The PR records.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Their claims in PR key then ref order; none for unknown or removed PRs.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOfMany(IReadOnlyList<RecordKey> prs, CancellationToken cancellationToken);

    /// <summary>Reads the claims of live PR records whose ref names a doc id (in any form of the ref), whatever the PR's audience.</summary>
    /// <param name="slug">The doc id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The claims in PR then ref order.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsNaming(string slug, CancellationToken cancellationToken);
}
#pragma warning restore MA0182
