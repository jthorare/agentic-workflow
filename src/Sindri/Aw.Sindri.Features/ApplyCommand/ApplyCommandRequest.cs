using System.Text.Json.Serialization;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// A command on an instance (engine spec rev 8 §5.2, §5.8, §6.3 'POST /workflow/instances/{id}/commands'): 'workflow
/// answer' in its variants, 'workflow command &lt;verb&gt;', 'workflow cancel', 'takeover' or 'handback', sent as the Update its kind names with
/// the nonce as its id.
/// </summary>
/// <param name="Instance">The instance's address, or a bare item for the root of its latest attempt.</param>
/// <param name="Kind">Answer, command, cancel, takeover, handback or rewind.</param>
/// <param name="Answer">The answer's variant, for an answer.</param>
/// <param name="Text">The answer's text ('--text', '--file', '--resume --text'), at most 1 MiB.</param>
/// <param name="Amount">The rounds of '--grant n' or the tokens of '--grant-tokens n'.</param>
/// <param name="Verb">The recipe verb, for a command.</param>
/// <param name="Nonce">The command's nonce; null to have one issued.</param>
/// <param name="Challenge">Accepted and ignored, so an older client still works (owner decision q93).</param>
/// <param name="To">The state to re-enter, for a rewind (#1270).</param>
public sealed record ApplyCommandRequest(
    string Instance,
    ApplyCommandKind Kind,
    AnswerVariant? Answer = null,
    string? Text = null,
    int? Amount = null,
    CommandVerb? Verb = null,
    CommandNonce? Nonce = null,
    ChallengeAnswer? Challenge = null,
    string? To = null) : ICommand<ApplyCommandResponse>, IClassifiedCommand
{
    /// <summary>True when <see cref="Instance"/> is a route segment (a client never sends an address there); set by the endpoint, never by a client: the validator then requires an instance id, after authorization has run.</summary>
    [JsonIgnore]
    public bool FromRoute { get; init; }

    /// <summary>
    /// The catalog variant (engine spec §6.2, UI spec E-12): 'answer-&lt;variant&gt;' ('answer-text', 'answer-grant' …),
    /// 'command' for every recipe verb, 'cancel', 'takeover' or 'handback'. An answer without a variant is 'answer', which no row names, so it is
    /// classified protected (fail-closed) and then refused as malformed.
    /// </summary>
    [JsonIgnore]
    public string Variant => Kind switch
    {
        ApplyCommandKind.Answer => Answer is { } variant ? "answer-" + AnswerName(variant) : "answer",
        ApplyCommandKind.Command => "command",
        ApplyCommandKind.Takeover => "takeover",
        ApplyCommandKind.Handback => "handback",
        ApplyCommandKind.Rewind => "rewind",
        ApplyCommandKind.Cancel or _ => "cancel",
    };

    /// <summary>A grant, a token grant or an accept, whatever the kind says: protected (W22).</summary>
    [JsonIgnore]
    public bool CarriesProtectedFields => Answer is AnswerVariant.Grant or AnswerVariant.GrantTokens or AnswerVariant.Accept;

    private static string AnswerName(AnswerVariant variant) => variant switch
    {
        AnswerVariant.Text => "text",
        AnswerVariant.Grant => "grant",
        AnswerVariant.GrantTokens => "grant-tokens",
        AnswerVariant.Accept => "accept",
        AnswerVariant.Reject => "reject",
        AnswerVariant.Resume => "resume",
        _ => "unknown",
    };
}
