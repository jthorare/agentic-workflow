using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SetupOnyx;

/// <summary>
/// Set the managed Onyx up from the app (POST /sindri/onyx/setup, 'sindri onyx up'; setup-once spec §5.6; desk): a job of kind 'SetupOnyx', key 'onyx'. It brings the stack up (Onyx spec §4.1 steps 1 to 6), then parks on the signal 'onyx-account' for
/// the owner's login (ConnectOnyx), and finishes steps 7 to 10 with it. It takes no input.
/// </summary>
public sealed record SetupOnyxRequest : IJob<SetupOnyxResult>;
