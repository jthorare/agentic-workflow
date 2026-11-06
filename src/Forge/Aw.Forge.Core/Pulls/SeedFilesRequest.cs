using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Sets the changed files of a pull request in a fake or file forge.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="Files">The files; they replace any seeded before.</param>
/// <param name="CapReached">Whether Files should say the forge's file cap was hit.</param>
public sealed record SeedFilesRequest(ForgeRef Pull, IReadOnlyList<SeedFile> Files, bool CapReached = false);
