namespace Aw.Context.Core.Library;

/// <summary>Which docs a listing returns; every part is optional and the parts add up.</summary>
/// <param name="Root">Only this root (<c language="text">aw</c>, a vault's name, <c language="text">repo:&lt;name&gt;</c>).</param>
/// <param name="Project">Only docs of this project.</param>
/// <param name="Repo">Only docs of this repo (<c language="text">owner/name</c>): those whose <c language="text">repos:</c> name it, and the docs committed to it.</param>
/// <param name="ErrorsOnly">Only docs with an error.</param>
public sealed record LibraryDocFilter(string? Root = null, string? Project = null, string? Repo = null, bool ErrorsOnly = false);
