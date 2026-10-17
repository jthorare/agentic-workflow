namespace Aw.Sindri.Features.OnyxUp;

/// <summary>Takes this box from nothing to the pinned Onyx running on 127.0.0.1: preflight, fetch, config, pull, start and readiness (spec §4.1 steps 1-5). Idempotent and resumable; the verb behind it is the human's.</summary>
public sealed record OnyxUpRequest;
