namespace Aw.Sindri.Features.RestartServe;

/// <summary>What the restart did.</summary>
/// <param name="Pid">The serve that is stopping.</param>
/// <param name="FromBuild">The stamp it ran.</param>
/// <param name="ToBuild">The stamp the relauncher starts.</param>
/// <param name="KeptRuns">The open agent runs left running.</param>
/// <param name="Relaunch">'scheduled'.</param>
public sealed record RestartServeResponse(int Pid, string FromBuild, string ToBuild, IReadOnlyList<KeptRun> KeptRuns, string Relaunch);
