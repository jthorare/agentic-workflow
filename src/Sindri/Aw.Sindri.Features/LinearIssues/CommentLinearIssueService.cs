using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// Posts a comment as the owner (Linear hub spec §5.3; desk). In order (an agent session may post too, agent-first PR 2): the key and client id are read (SND-CLI-003); the issue's team must be a picked one (SND-LINEAR-003); Context posts it with the client id,
/// so a retry never posts twice. The answer is the comment as the issue now holds it, read back through <see cref="GetLinearIssueService"/>; when the store missed it (SND-LINEAR-013) the answer carries the text just sent.
/// A write that Linear never confirmed is SND-LINEAR-011 with the client id to retry with.
/// </summary>
/// <param name="scopes">The owner's scope.</param>
/// <param name="engine">Context's Linear writes.</param>
/// <param name="teams">The picked-team check.</param>
/// <param name="issues">The issue read the answer is taken from.</param>
/// <param name="clock">Stamps a comment the store did not return.</param>
/// <param name="recorder">Records the write under the caller's actor.</param>
internal sealed class CommentLinearIssueService(LinearScopes scopes, IContextLinearWrites engine, LinearTeamGate teams, IService<GetLinearIssueRequest, GetLinearIssueResponse> issues, TimeProvider clock, ILinearWriteRecorder recorder)
    : IService<CommentLinearIssueRequest, CommentLinearIssueResponse>
{
    private const string NotStored = "SND-LINEAR-013";

    public async ValueTask<Result<CommentLinearIssueResponse>> Handle(CommentLinearIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = LinearIssueKey.Parse(request.Key);
        var client = LinearClientId.Parse(request.ClientId);
        if (key.IsError || client.IsError)
        {
            return SindriErrors.InvalidInput.With((key.IsError ? key.Error : client.Error).Message);
        }

        var scope = await scopes.Write(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var team = await teams.Picked(key.Value.Team, cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var sent = await engine.Comment(key.Value, request.Body, client.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error.Code == LinearHubErrors.NoAnswer.Code ? Unconfirmed(key.Value, client.Value) : LinearWriteFailures.Of(sent.Error);
        }

        if (sent.Value is LinearWriteApplied or LinearWriteAppliedNotStored
            && await recorder.Record(EventTypes.LinearCommentPosted, key.Value.Team, key.Value.ToString(), client.Value.Value, fields: null, cancellationToken).ConfigureAwait(false) is { } failed)
        {
            return failed;
        }

        return sent.Value switch
        {
            LinearWriteUnknown => Unconfirmed(key.Value, client.Value),
            LinearWriteAppliedNotStored => await Applied(request, client.Value, NotStored, slow: false, cancellationToken).ConfigureAwait(false),
            LinearWriteApplied applied => await Applied(request, client.Value, warning: null, applied.FoundOnReread, cancellationToken).ConfigureAwait(false),
            _ => Unconfirmed(key.Value, client.Value),
        };
    }

    private static Error Unconfirmed(LinearIssueKey key, LinearClientId client) =>
        LinearWriteFailures.NoAnswer($"Linear didn't confirm the comment on {key} (fix: retry; the same client id is never posted twice)", client.Value);

    // why: slow is Context's FoundOnReread: Linear gave no answer and only the one re-read found the comment
    private async ValueTask<CommentLinearIssueResponse> Applied(CommentLinearIssueRequest request, LinearClientId client, string? warning, bool slow, CancellationToken cancellationToken)
    {
        var read = await issues.Handle(new GetLinearIssueRequest(request.Key), cancellationToken).ConfigureAwait(false);
        var sheet = read.IsSuccess ? read.Value : null;
        var held = sheet?.Comments.FirstOrDefault(c => string.Equals(c.Id, client.Value, StringComparison.Ordinal))
            ?? sheet?.Comments.FirstOrDefault(c => c.Sindri && string.Equals(c.Body, request.Body, StringComparison.Ordinal));
        var comment = held ?? new LinearCommentView(client.Value, sheet?.Viewer ?? new LinearPersonView(string.Empty, "You"), clock.GetUtcNow(), request.Body, Sindri: true);
        return new CommentLinearIssueResponse("applied", client.Value, comment, Slow: slow, warning);
    }
}
