namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/profile/approve's body: the live profile's full hash. One click sends it, once.</summary>
/// <param name="Hash">The full hash to approve (the profile row's subject).</param>
internal sealed record ApproveProfileRequest(string Hash);
