
namespace Aw.Sindri.Core.Profile;

/// <summary>What the detector found in a repo.</summary>
/// <param name="Toolchains">Each toolchain found, in a fixed order (Node, Python, Go, dotnet).</param>
/// <param name="Settings">The entries of all of them together, or null when no toolchain was found.</param>
public sealed record ToolchainOffer(IReadOnlyList<DetectedToolchain> Toolchains, SandboxRepoSettings? Settings);
