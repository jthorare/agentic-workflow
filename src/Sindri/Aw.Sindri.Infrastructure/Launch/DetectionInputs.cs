namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What <see cref="TerminalDetection.Detect"/> reads: the environment and two probes, all injectable so a test needs no real terminal.</summary>
/// <param name="GetEnv">Reads an environment variable.</param>
/// <param name="OnPath">True when a program resolves on the login PATH (<c lang="text">bash -lc 'command -v -- "$1"' _ prog</c>).</param>
/// <param name="WarpPresent">True when Warp can open a launch file here.</param>
/// <param name="Templates">terminals.json's rows.</param>
internal sealed record DetectionInputs(Func<string, string?> GetEnv, Func<string, CancellationToken, ValueTask<bool>> OnPath, Func<CancellationToken, ValueTask<bool>> WarpPresent, IReadOnlyList<TerminalTemplate> Templates);
