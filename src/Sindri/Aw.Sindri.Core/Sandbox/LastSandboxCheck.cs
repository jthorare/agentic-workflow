namespace Aw.Sindri.Core.Sandbox;

/// <summary>The newest sandbox check this serve ran, kept in memory so the status read never runs a probe (sandbox spec §8). Nothing is written to disk: after a restart no check has run.</summary>
public sealed class LastSandboxCheck
{
    private SandboxCheckRecord? _latest;

    /// <summary>Gets the newest finished check, or null when none has run since serve started.</summary>
    public SandboxCheckRecord? Latest => Volatile.Read(ref _latest);

    /// <summary>Keeps <paramref name="record"/> as the newest check.</summary>
    /// <param name="record">The finished check.</param>
    public void Record(SandboxCheckRecord record) => Volatile.Write(ref _latest, record);
}
