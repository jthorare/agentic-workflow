
namespace Aw.Sindri.Core.Launch;

/// <summary>The 'launch.probed' payload (UI spec E-3, launcher L-4): what this box can launch now. Also what <see cref="ILauncher.Probe"/> returns.</summary>
/// <param name="Terminal">The configured viewer.</param>
/// <param name="Host">The session host an attended launch uses.</param>
/// <param name="Targets">Every catalog target, built-ins first.</param>
/// <param name="Repos">Every profile repo.</param>
/// <param name="Stopped">Why probing stopped (the live profile file is invalid, with its path), or null while it runs; the other fields are then the last probe's (#3594).</param>
public sealed record LaunchProbed(TerminalFacts Terminal, HostFacts Host, IReadOnlyList<TargetFacts> Targets, IReadOnlyList<RepoFacts> Repos, string? Stopped = null);
