namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>POST /sindri/sandbox/allow's answer (wire contract item 8): the edit is a draft until the owner approves the profile, unless the approved profile already has it.</summary>
/// <param name="State">'needs-approval' (the profile drift approval takes over) or 'allowed' (the approved profile already has it).</param>
/// <param name="Repo">The repo the entry is under.</param>
/// <param name="Entry">The host, or the home path as the profile holds it.</param>
internal sealed record AllowView(string State, string Repo, string Entry);
