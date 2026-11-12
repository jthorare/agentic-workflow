using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>
/// Restart Sindri (first-run spec decision 11, <c lang="text">POST /sindri/restart</c>): the owner's. It reads the open set (<c lang="text">GET /sindri/runs/open</c>), refuses with SND-SETUP-009 when anything is open, and
/// otherwise stops serve the way <c lang="text">POST /sindri/stop</c> does and answers 202 with the old serve's <see cref="RestartProcessResponse"/> (the same fields as the stop's); the caller waits for that pid to exit and starts the installed build.
/// </summary>
internal sealed record RestartProcessRequest() : ICommand<RestartProcessResponse>;
