namespace Aw.Sindri.Ui.Api;

/// <summary>The live profile's hashes: what a sheet sends back as its ifHash, so a write the profile moved under is refused (SND-PROFILE-003) and nothing else.</summary>
/// <param name="LiveHash">The live hash, or null when none was read.</param>
/// <param name="FileHash">The hash of the profile file on disk at the read, or null; a sheet sends it as its ifHash, since the live hash can lag the file (#1826).</param>
internal sealed record ProfileHashes(string? LiveHash, string? FileHash = null);
