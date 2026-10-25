namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// What 'session wrap' needs to hold a sandboxed session to its limits (sandbox spec §3.3, SB7): the launcher writes it beside the plan and names it with '--limits'.
/// A session with no limits file runs unwatched, as before.
/// </summary>
/// <param name="Unit">The session's scope unit.</param>
/// <param name="WallSeconds">The wall limit in seconds (the Step's timeout plus the grace), or null for a session with none (an attended one).</param>
/// <param name="WallGraceSeconds">The grace inside <paramref name="WallSeconds"/>, kept for the doctor's text.</param>
/// <param name="ParkMaxSeconds">The longest single suspension the loose ceiling allows.</param>
/// <param name="MemoryMaxBytes">The scope's MemoryMax, reported as the limit when the kernel kills for memory.</param>
internal sealed record WrapLimits(string Unit, long? WallSeconds, long WallGraceSeconds, long ParkMaxSeconds, long MemoryMaxBytes);
