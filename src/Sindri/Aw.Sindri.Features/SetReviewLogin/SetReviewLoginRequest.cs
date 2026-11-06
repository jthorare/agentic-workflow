using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SetReviewLogin;

/// <summary>Save the owner's GitHub login as 'review.login' in the live profile (PR review page spec §6.1, §9): the Reviews tab's first-run field. Human only.</summary>
/// <param name="Login">The GitHub login.</param>
public sealed record SetReviewLoginRequest(string Login) : ICommand<SetReviewLoginResponse>;
