namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>GET /sindri/sandbox/denials?item= (wire contract item 8): the denial counters, one per launch, newest first.</summary>
/// <param name="Launches">The launches' counters.</param>
internal sealed record DenialsView(IReadOnlyList<DenialLaunchView> Launches);
