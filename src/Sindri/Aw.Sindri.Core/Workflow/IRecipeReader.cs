namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Reads recipe text (spec §3.1): the YAML 1.2 core schema, refused features (duplicate keys, anchors, aliases, merge
/// keys, explicit tags, more than 256 KiB, nesting deeper than 32), normalization of short forms to the long form,
/// typed binding with unmapped members refused, the hash over the long form, and every node's position. No file I/O:
/// the caller passes the text.
/// </summary>
public interface IRecipeReader
{
    /// <summary>Reads <paramref name="text"/>.</summary>
    /// <param name="text">The recipe file's contents.</param>
    /// <param name="file">The file's name, as findings show it.</param>
    /// <returns>The bound document, or the findings that stopped it.</returns>
    public RecipeReading Read(string text, string file);
}
