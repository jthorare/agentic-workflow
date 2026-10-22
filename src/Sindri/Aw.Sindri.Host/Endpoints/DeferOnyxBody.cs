namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/onyx/defer: '{deferred}'. A missing one is a 400, not a quiet "false".</summary>
/// <param name="Deferred">True for "Not now", false to offer Onyx again.</param>
internal sealed record DeferOnyxBody(bool? Deferred);
