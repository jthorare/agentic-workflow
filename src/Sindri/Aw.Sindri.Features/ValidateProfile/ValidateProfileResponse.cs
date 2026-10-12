using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ValidateProfile;

/// <summary>A valid profile's hash and where it was read from.</summary>
/// <param name="Hash">Its hash (what 'profile approve' would record).</param>
/// <param name="Source">The file.</param>
/// <param name="Approved">Whether that hash is the latest approval.</param>
public sealed record ValidateProfileResponse(ProfileHash Hash, string Source, bool Approved);
