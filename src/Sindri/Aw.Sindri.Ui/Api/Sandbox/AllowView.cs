namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>POST /sindri/sandbox/allow's answer (wire contract item 8): the live profile holds the entry once the answer arrives.</summary>
/// <param name="State">The host's word for the outcome; the app treats a non-error answer as allowed.</param>
/// <param name="Repo">The repo the entry is under.</param>
/// <param name="Entry">The host, or the home path as the profile holds it.</param>
internal sealed record AllowView(string State, string Repo, string Entry);
