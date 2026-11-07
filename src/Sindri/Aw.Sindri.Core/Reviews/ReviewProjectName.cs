namespace Aw.Sindri.Core.Reviews;

/// <summary>The project whose <c language="text">review.projects[]</c> entry names a repo.</summary>
/// <param name="Key">The key the entry holds ("prj_…" or "linear:project/…").</param>
/// <param name="Name">The project's name on the Projects page.</param>
public sealed record ReviewProjectName(string Key, string Name);
