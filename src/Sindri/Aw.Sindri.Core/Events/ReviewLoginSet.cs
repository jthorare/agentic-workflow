using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'review.login-set' payload (PR review page spec): a caller saved 'review.login' in the live profile. Key 'review.login-set:&lt;command id&gt;'; the envelope's actor is the caller.</summary>
/// <param name="CommandId">The 'SetReviewLogin' command's id.</param>
/// <param name="Login">The login that was saved.</param>
public sealed record ReviewLoginSet(CommandId CommandId, string Login);
