namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where and how <see cref="Launcher"/> works (API P10a, U7): its files, the sindri executable it wraps agents in, and its stage bounds.</summary>
/// <param name="Home">Sindri's home: per-launch files live under <c lang="text">launch/</c> in it.</param>
internal sealed record LauncherOptions(string Home)
{
    /// <summary>The executable the agent runs under (<c lang="text">sindri session wrap</c>), as the host finds it.</summary>
    public string SindriExecutable { get; init; } = "sindri";

    /// <summary>AW_SINDRI_HOME for the agent when it isn't the default (so the id hook writes under the right home), else null.</summary>
    public string? SindriHome { get; init; }

    /// <summary>The host start bound (launcher spec §3: 4 s).</summary>
    public TimeSpan HostDeadline { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>The viewer observation window (launcher spec §3: 3 s).</summary>
    public TimeSpan ViewerWindow { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>The per-launch secret hashes: a launcher-owned directory the doctor and the scrubber skip.</summary>
    public string SecretsDirectory => Path.Combine(Home, "launch", "secrets");

    /// <summary>The flat per-launch files the id hook and 'session wrap' share (<c lang="text">&lt;id&gt;.capture.json</c>, <c lang="text">&lt;id&gt;.session-id</c>; launcher L-9).</summary>
    public string LaunchesDirectory => Path.Combine(Home, Core.Launch.LaunchFiles.Directory);

    /// <summary>The per-launch Step policy files (0600, deleted with the launch).</summary>
    public string PolicyDirectory => Path.Combine(Home, "launch", "policy");
}
