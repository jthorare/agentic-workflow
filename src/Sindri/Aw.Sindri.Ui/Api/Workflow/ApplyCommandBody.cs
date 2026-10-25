namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The body of POST /sindri/workflow/instances/{id}/commands (engine spec rev 8 §5.2, §6.3): a command on an instance. The route names
/// the instance and wins over the body's. A token grant carries the amount.
/// </summary>
/// <param name="Instance">The instance's id (an address for a grant).</param>
/// <param name="Kind">The command: an answer, a cancel or a hand back.</param>
/// <param name="Answer">The answer's variant, for an answer; null otherwise.</param>
/// <param name="Text">The answer's text (a hint); null otherwise.</param>
/// <param name="Amount">The tokens granted, for a grant; null otherwise.</param>
/// <param name="Verb">The recipe verb, for a "command"; null otherwise.</param>
internal sealed record ApplyCommandBody(string Instance, CommandKind Kind, AnswerVariant? Answer = null, string? Text = null, int? Amount = null, string? Verb = null)
{
    /// <summary>A recipe verb (merge, check, rework …) on <paramref name="instance"/>.</summary>
    /// <param name="instance">The instance's id.</param>
    /// <param name="verb">The verb, as the instance's allowedCommands name it.</param>
    /// <returns>The body.</returns>
    internal static ApplyCommandBody ForVerb(string instance, string verb) => new(instance, CommandKind.Command, Verb: verb);

    /// <summary>The token grant of <paramref name="amount"/> on <paramref name="address"/>.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="amount">The tokens.</param>
    /// <returns>The body.</returns>
    internal static ApplyCommandBody GrantTokens(string address, int amount) => new(address, CommandKind.Answer, AnswerVariant.GrantTokens, Text: null, Amount: amount);
}
