using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Where evidence may go. Every write still asks first; unknown provenance never leaves the machine.</summary>
/// <remarks>Seeded and scrubbed data go to both Linear and the PR: only a run whose data may be real stays local.</remarks>
public static class PublishPolicy
{
    /// <summary>Linear takes seeded and scrubbed data (after an explicit ask).</summary>
    /// <param name="provenance">The data's provenance.</param>
    /// <returns>Whether images may go to Linear.</returns>
    public static bool CanUploadToLinear(Provenance provenance) => provenance is Provenance.Seeded or Provenance.Scrubbed;

    /// <summary>GitHub comments carry image URLs for seeded and scrubbed data, with an approved uploader.</summary>
    /// <param name="provenance">The data's provenance.</param>
    /// <param name="hasApprovedUploader">Whether an approved uploader is configured.</param>
    /// <returns>Whether images may be linked from a PR.</returns>
    public static bool CanEmbedImagesInPr(Provenance provenance, bool hasApprovedUploader) =>
        provenance is Provenance.Seeded or Provenance.Scrubbed && hasApprovedUploader;
}
