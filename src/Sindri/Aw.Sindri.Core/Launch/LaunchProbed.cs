
namespace Aw.Sindri.Core.Launch;

/// <summary>The 'launch.probed' payload (UI spec E-3, launcher L-4): what this box can launch now. Also what <see cref="ILauncher.Probe"/> returns.</summary>
/// <param name="Terminal">The configured viewer.</param>
/// <param name="Host">The session host an attended launch uses.</param>
/// <param name="Targets">Every catalog target, built-ins first.</param>
/// <param name="Repos">Every profile repo.</param>
public sealed record LaunchProbed(TerminalFacts Terminal, HostFacts Host, IReadOnlyList<TargetFacts> Targets, IReadOnlyList<RepoFacts> Repos);
