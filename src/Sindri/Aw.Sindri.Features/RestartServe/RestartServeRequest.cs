using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RestartServe;

/// <summary>Restart serve onto the installed build (#3052). Open agent runs are never a reason to refuse: they are separate processes and their workflows live in Temporal's file.</summary>
public sealed record RestartServeRequest : ICommand<RestartServeResponse>;
