namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One package at one version, named the way OSV.dev names ecosystems ('npm', 'NuGet', 'Go', 'crates.io', 'PyPI').</summary>
/// <param name="Ecosystem">The ecosystem.</param>
/// <param name="Name">The package's name.</param>
/// <param name="Version">The version in use.</param>
public sealed record VulnPackage(string Ecosystem, string Name, string Version);
