namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>POST /sindri/reviews/login's body (review page spec §6.1): the GitHub login to review as.</summary>
/// <param name="Login">The login the owner typed.</param>
internal sealed record ReviewLoginSave(string Login);
