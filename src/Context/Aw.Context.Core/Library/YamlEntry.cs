namespace Aw.Context.Core.Library;

/// <summary>One top-level key of the front matter: a scalar, or a list (flow <c language="text">[a, b]</c> or block).</summary>
/// <param name="Key">The key.</param>
/// <param name="Line">Its line in the file (from 1).</param>
/// <param name="Scalar">The value when it is a single one; null for a list.</param>
/// <param name="List">The values when it is a list; null for a scalar.</param>
internal sealed record YamlEntry(string Key, int Line, YamlScalar? Scalar, IReadOnlyList<YamlScalar>? List);
