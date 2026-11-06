namespace Aw.Context.Core.Library;

/// <summary>What <see cref="DesignDocParser"/> makes of one file: the shape <c language="text">schemas/library/design-doc.schema.json</c> defines.</summary>
/// <param name="ParserVersion">The parser's version (<see cref="DesignDocParser.Version"/>).</param>
/// <param name="FrontMatter">The front matter; null when the file has none the parser reads (a near miss, an unclosed block, no front matter).</param>
/// <param name="Items">The items in document order; empty when any error is present.</param>
/// <param name="Refs">The links between items; empty when any error is present.</param>
/// <param name="Errors">The errors, by line.</param>
public sealed record ParsedDesignDoc(
    int ParserVersion,
    DesignDocFrontMatter? FrontMatter,
    IReadOnlyList<DesignItem> Items,
    IReadOnlyList<DesignRef> Refs,
    IReadOnlyList<DesignDocError> Errors);
