using Aw.Context.Core.Records;

namespace Aw.Context.Core.Library;

/// <summary>A PR record's side rows (spec 2026-11-04 §4.4): the refs of its <c language="text">Covers:</c> line, which the <see cref="WriterId"/> writer stores as <c language="text">design_doc_claim</c> rows.</summary>
/// <param name="Refs">The refs as written; empty clears the claims the PR had.</param>
public sealed record PrClaimSideRows(IReadOnlyList<string> Refs) : SideRows(WriterId)
{
    /// <summary>The writer's id.</summary>
    public const string WriterId = "pr-claims";
}
