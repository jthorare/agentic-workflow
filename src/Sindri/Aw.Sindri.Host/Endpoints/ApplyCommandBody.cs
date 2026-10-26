using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflow/instances/{instanceId}/commands: the command without its instance, which the route names.</summary>
/// <param name="Kind">Answer, command, cancel, takeover or handback.</param>
/// <param name="Answer">The answer's variant, for an answer.</param>
/// <param name="Text">The answer's text.</param>
/// <param name="Amount">The rounds or tokens of a grant.</param>
/// <param name="Verb">The recipe verb, for a command.</param>
/// <param name="Nonce">The command's nonce; null to have one issued.</param>
/// <param name="Challenge">Accepted and ignored, so an older client still works.</param>
internal sealed record ApplyCommandBody(
    ApplyCommandKind Kind,
    AnswerVariant? Answer = null,
    string? Text = null,
    int? Amount = null,
    CommandVerb? Verb = null,
    CommandNonce? Nonce = null,
    ChallengeAnswer? Challenge = null)
{
    /// <summary>The request for <paramref name="instance"/>, marked as a route's: its validator checks the instance id after authorization.</summary>
    /// <param name="instance">The route value, unparsed.</param>
    /// <returns>The request.</returns>
    internal ApplyCommandRequest ToRequest(string instance) => new(instance, Kind, Answer, Text, Amount, Verb, Nonce, Challenge) { FromRoute = true };
}
