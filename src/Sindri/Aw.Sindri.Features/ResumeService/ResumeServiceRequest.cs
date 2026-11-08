using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ResumeService;

/// <summary>Resume a background service (POST /sindri/services/{name}/resume, 'sindri services resume'; API spec §3.2): desk class, appends 'service.resumed'.</summary>
/// <param name="Name">The service, one of the approved profile's.</param>
public sealed record ResumeServiceRequest(ServiceName Name) : ICommand<CommandAccepted>;
