namespace Aw.Sindri.Features.SetReviewLogin;

/// <summary>What the login save did.</summary>
/// <param name="Login">The login now in the profile.</param>
/// <param name="Approved">True when the live profile is approved after the save: the login was the only waiting change and the save approved it. False when other changes wait (the owner approves them in Settings).</param>
public sealed record SetReviewLoginResponse(string Login, bool Approved);
