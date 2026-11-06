using Aw.Context.Core.Records;

namespace Aw.Context.Core.Library;

/// <summary>A design doc record's side rows (spec 2026-11-04 §5.3): its parse, which the <see cref="WriterId"/> writer stores as <c language="text">design_doc</c>, <c language="text">design_doc_item</c> and <c language="text">design_doc_ref</c> rows.</summary>
/// <param name="Doc">The parse; null when the record is not (or is no longer) a design doc, which clears the rows it had.</param>
public sealed record DesignDocSideRows(ParsedDesignDoc? Doc) : SideRows(WriterId)
{
    /// <summary>The writer's id.</summary>
    public const string WriterId = "design-doc";
}
