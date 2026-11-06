using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The design-doc library's reads (PR review page spec 2026-11-04 §5.5): docs, their items and the PRs that claim them. Every read takes the caller's <see cref="AudienceScope"/>: a doc, its items and its refs are read through
/// the doc record's audience, a claim through its PR's and its doc's. Nothing here calls a feed or writes; a record the caller may not read is as good as missing.
/// </summary>
public interface IContextLibrary
{
    /// <summary>Lists the docs the caller may read, in key order, each with the errors decided on read (a shared doc id: the live doc at the lowest key keeps it).</summary>
    /// <param name="filter">Which docs.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The docs, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken);

    /// <summary>Reads one doc.</summary>
    /// <param name="key">The doc record's key.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The doc, or null when there is none or the caller may not read it.</returns>
    public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken);

    /// <summary>Reads a doc's items in document order.</summary>
    /// <param name="docKey">The doc record's key.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The items; none when the doc is unknown, itemless or not readable by the caller.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken);

    /// <summary>Resolves refs against the docs the caller may read: a full ref (<c language="text">doc:&lt;scope&gt;/&lt;doc id&gt;#&lt;item&gt;</c>) by its scope, a short one (<c language="text">&lt;doc id&gt;#&lt;item&gt;</c>) in the PR repo's own root, then in the aw and vault docs whose <c language="text">repos:</c> name that repo.</summary>
    /// <param name="refs">The refs as written.</param>
    /// <param name="repo">The PR's repo (<c language="text">owner/name</c>) for short refs; null resolves only full refs.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One resolution per ref, in order.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken);

    /// <summary>Reads a PR's claims and what they resolve to now.</summary>
    /// <param name="prKey">The PR record's key.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The claims in ref order; none when the PR is unknown or the caller may not read it. A claim on a doc the caller may not read is missing.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken);

    /// <summary>Reads the claims on a doc's items: from open and merged PRs only (a closed PR's claim is not shown).</summary>
    /// <param name="docKey">The doc record's key.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The claims in PR then ref order; none when the doc is unknown or the caller may not read it. A PR the caller may not read is left out.</returns>
    public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken);
}
