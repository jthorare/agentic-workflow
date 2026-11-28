using Aw.Judge.Core.Images;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>One Jev request: an excerpt of a file and the traits asked about it. An edited image file has no text excerpt; its picture rides along instead.</summary>
/// <param name="Source">The excerpt.</param>
/// <param name="Batch">The traits.</param>
internal sealed record TraitTask(DiffSource Source, IReadOnlyList<Trait> Batch)
{
    /// <summary>The picture the excerpt stands for (an edited PNG or JPEG), or none.</summary>
    public IReadOnlyList<ImageAttachment> Images { get; init; } = [];

    /// <summary>The top-level directory of the repo the excerpt's file is in; the source's path is relative to it.</summary>
    public string Root { get; init; } = "";

    /// <summary>The source's hash (<see cref="TraitVerdictKeys.SourceHash"/>), or empty before the verdict cache has looked; logged as the first 16 characters.</summary>
    public string Hash { get; init; } = "";
}
