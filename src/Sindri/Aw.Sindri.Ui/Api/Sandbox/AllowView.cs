namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>POST /sindri/sandbox/allow's answer (wire contract item 8): the edit is a draft until the owner approves the profile.</summary>
/// <param name="State">'needs-approval'.</param>
/// <param name="Approval">The profile approval's id; the app reads Allowed or Refused from its outcome.</param>
internal sealed record AllowView(string State, string Approval);
