namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a publish job did.</summary>
/// <param name="Version">The held draft's number, or 0 when nothing was held.</param>
/// <param name="Reason">The publish reason (IndexReady, NoIndex, IndexBroken, FirstMap, WaitOff, TimedOut), the drop reason (Gone, AlreadyCurrent), 'Waiting' when the decision said wait after all, or 'NotHeld'.</param>
/// <param name="Code">SND-CONTEXT-190 when the draft was no longer the expected one; null otherwise.</param>
public sealed record PublishContextMapResult(int Version, string Reason, string? Code);
