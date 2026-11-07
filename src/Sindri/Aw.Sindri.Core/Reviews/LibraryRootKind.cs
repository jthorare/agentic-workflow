namespace Aw.Sindri.Core.Reviews;

/// <summary>Which kind of folder a library root is (Settings → Library). The declaration order is the order the page lists them in.</summary>
public enum LibraryRootKind
{
    /// <summary>The state folder, <c language="text">aw</c>.</summary>
    Aw,

    /// <summary>A vault of notes the profile names.</summary>
    Vault,

    /// <summary>A profile repo's docs folder.</summary>
    Repo,
}
