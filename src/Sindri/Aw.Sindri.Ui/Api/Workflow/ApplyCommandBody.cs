namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The body of POST /sindri/workflow/instances/{id}/commands (engine spec rev 8 §5.2, §6.3): a command on an instance. The route names
/// the instance and wins over the body's.
/// </summary>
/// <param name="Instance">The instance's id.</param>
/// <param name="Kind">The command: an answer, a cancel or a hand back.</param>
/// <param name="Answer">The answer's variant, for an answer; null otherwise.</param>
/// <param name="Text">The answer's text (a hint); null otherwise.</param>
internal sealed record ApplyCommandBody(string Instance, CommandKind Kind, AnswerVariant? Answer = null, string? Text = null);
