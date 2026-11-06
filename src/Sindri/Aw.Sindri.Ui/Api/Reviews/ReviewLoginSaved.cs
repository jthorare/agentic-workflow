namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>POST /sindri/reviews/login's answer.</summary>
/// <param name="Login">The saved login.</param>
/// <param name="Approved">True when the setting took effect; false while the profile change waits for the owner's approval.</param>
internal sealed record ReviewLoginSaved(string Login, bool Approved);
