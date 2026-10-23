namespace Aw.Sindri.Features.OnyxAttach;

/// <summary>`sindri onyx attach &lt;url&gt;` (deployable-context spec B.3, C.3): use the Onyx that already runs at a cluster address instead of standing one up. The human's verb; the account stage then runs against it.</summary>
/// <param name="Url">Onyx's base URL, such as http://onyx-api-service.sindri.svc:8080; a host in AW_SINDRI_ONYX_HOSTS, pod mode only.</param>
public sealed record OnyxAttachRequest(string Url);
