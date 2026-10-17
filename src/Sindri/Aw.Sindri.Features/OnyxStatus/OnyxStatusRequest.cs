namespace Aw.Sindri.Features.OnyxStatus;

/// <summary>Whether this box can run the pinned Onyx. Read-only; runs in the calling process, so it works when the control plane is down and for an agent session.</summary>
public sealed record OnyxStatusRequest;
