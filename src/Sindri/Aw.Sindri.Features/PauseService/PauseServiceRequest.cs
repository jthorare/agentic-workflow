using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.PauseService;

/// <summary>Pause a background service (POST /sindri/services/{name}/pause, 'sindri services pause'; API spec §3.2): desk class, appends 'service.paused'.</summary>
/// <param name="Name">The service, one of the approved profile's.</param>
public sealed record PauseServiceRequest(ServiceName Name) : ICommand<CommandAccepted>;
