namespace Aw.Context.Core.Library;

/// <summary>A parsed doc with where it lives, the input of <see cref="DesignDocScope.Check"/>.</summary>
/// <param name="Path">The doc's path; the lowest path keeps a shared doc id.</param>
/// <param name="Scope">Its scope key: <c language="text">aw/&lt;repo-slug&gt;</c> in the aw root, the vault name in a vault, <c language="text">repo:&lt;name&gt;</c> in a repo (the part of a full ref before the doc id).</param>
/// <param name="Doc">What the parser made of it.</param>
public sealed record ScopedDesignDoc(string Path, string Scope, ParsedDesignDoc Doc);
