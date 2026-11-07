using Aw.Context.Core.Linear;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// Posts a comment as the owner (Linear hub spec §5.3; desk). In order: a session is refused (SND-AUTH-002); the key and client id are read (SND-CLI-003); the issue's team must be a picked one (SND-LINEAR-003); Context posts it with the client id,
/// so a retry never posts twice. The answer is the comment as the issue now holds it, read back through <see cref="GetLinearIssueService"/>; when the store missed it (SND-LINEAR-013) the answer carries the text just sent.
/// A write that Linear never confirmed is SND-LINEAR-011 with the client id to retry with.
/// </summary>
/// <param name="scopes">The owner's scope.</param>
/// <param name="engine">Context's Linear writes.</param>
/// <param name="teams">The picked-team check.</param>
/// <param name="issues">The issue read the answer is taken from.</param>
/// <param name="clock">Stamps a comment the store did not return.</param>
internal sealed class CommentLinearIssueService(LinearScopes scopes, IContextLinearWrites engine, LinearTeamGate teams, IService<GetLinearIssueRequest, GetLinearIssueResponse> issues, TimeProvider clock)
    : IService<CommentLinearIssueRequest, CommentLinearIssueResponse>
{
    private const string NotStored = "SND-LINEAR-013";

    public async ValueTask<Result<CommentLinearIssueResponse>> Handle(CommentLinearIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (scopes.IsSession())
        {
            return SindriErrors.AuthRefused.With(LinearWriteFailures.Refusal);
        }

        var key = LinearIssueKey.Parse(request.Key);
        var client = LinearClientId.Parse(request.ClientId);
        if (key.IsError || client.IsError)
        {
            return SindriErrors.InvalidInput.With((key.IsError ? key.Error : client.Error).Message);
        }

        var team = await teams.Picked(key.Value.Team, cancellationToken).ConfigureAwait(false);
        var scope = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (team.IsError || scope.IsError)
        {
            return team.IsError ? team.Error : scope.Error;
        }

        var sent = await engine.Comment(key.Value, request.Body, client.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error.Code == LinearHubErrors.NoAnswer.Code ? Unconfirmed(key.Value, client.Value) : LinearWriteFailures.Of(sent.Error);
        }

        return sent.Value switch
        {
            LinearWriteUnknown => Unconfirmed(key.Value, client.Value),
            LinearWriteAppliedNotStored => await Applied(key.Value, client.Value, request.Body, NotStored, cancellationToken).ConfigureAwait(false),
            _ => await Applied(key.Value, client.Value, request.Body, warning: null, cancellationToken).ConfigureAwait(false),
        };
    }

    private static Error Unconfirmed(LinearIssueKey key, LinearClientId client) =>
        LinearWriteFailures.NoAnswer($"Linear didn't confirm the comment on {key} (fix: retry; the same client id is never posted twice)", client.Value);

    // why: Slow is false until Context's outcome says whether the comment landed only on its one re-read; it doesn't yet
    private async ValueTask<CommentLinearIssueResponse> Applied(LinearIssueKey key, LinearClientId client, string body, string? warning, CancellationToken cancellationToken)
    {
        var read = await issues.Handle(new GetLinearIssueRequest(key.ToString()), cancellationToken).ConfigureAwait(false);
        var sheet = read.IsSuccess ? read.Value : null;
        var held = sheet?.Comments.FirstOrDefault(c => string.Equals(c.Id, client.Value, StringComparison.Ordinal))
            ?? sheet?.Comments.FirstOrDefault(c => c.Sindri && string.Equals(c.Body, body, StringComparison.Ordinal));
        var comment = held ?? new LinearCommentView(client.Value, sheet?.Viewer ?? new LinearPersonView(string.Empty, "You"), clock.GetUtcNow(), body, Sindri: true);
        return new CommentLinearIssueResponse("applied", client.Value, comment, Slow: false, warning);
    }
}
