namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The body of POST /sindri/workflow/instances/{id}/commands as far as the app still sends it: the takeover of an agent run, which PR 17's attach and take over
/// control keeps. The route names the instance and wins over the body's.
/// </summary>
/// <param name="Instance">The instance's id.</param>
/// <param name="Kind">The command.</param>
internal sealed record ApplyCommandBody(string Instance, CommandKind Kind);
