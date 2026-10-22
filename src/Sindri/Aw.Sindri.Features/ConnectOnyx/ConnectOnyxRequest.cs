using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ConnectOnyx;

/// <summary>
/// Wake the Onyx setup job that waits for the owner's login (POST /sindri/onyx/account; setup-once spec §5.6; desk). The record carries only the job id: the login rides in a one-request hand-over and then a single-use slot keyed by
/// this id, so it is not a field of anything that is audited, stored or serialized.
/// </summary>
/// <param name="Job">The 'SetupOnyx' job that is waiting on 'onyx-account'.</param>
public sealed record ConnectOnyxRequest(JobId Job) : ICommand<JobStartedResponse>;
