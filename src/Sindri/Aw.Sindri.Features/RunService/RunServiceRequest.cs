using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RunService;

/// <summary>Start a run of a background service now (POST /sindri/services/{name}/run, 'sindri services run'; API spec §3.2): a job-class command. It starts a job that runs the named service as 'service:&lt;name&gt;' and records the run's manual 'service.ran'.</summary>
/// <param name="Name">The service, one of the approved profile's.</param>
public sealed record RunServiceRequest(ServiceName Name) : IJob<ScheduledResult>;
