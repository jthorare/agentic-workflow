namespace Aw.Sindri.Core.Sessions;

/// <summary>A job that waits for a heavy-job slot (Agents page spec §3.2, slot wait).</summary>
/// <param name="Since">When the wait began.</param>
/// <param name="Held">How many slots are held.</param>
/// <param name="Cap">How many slots the box has.</param>
public sealed record AgentHeavyWait(DateTimeOffset Since, int Held, int Cap);
