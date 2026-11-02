namespace Aw.Sindri.Features.AllowDenial;

/// <summary>The answer of Allow.</summary>
/// <param name="State">Needs approval, or already allowed.</param>
/// <param name="Repo">The repo the entry is under.</param>
/// <param name="Entry">The host, or the home path as the profile holds it.</param>
public sealed record AllowDenialResponse(AllowDenialState State, string Repo, string Entry);
