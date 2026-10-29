using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.AllowDenial;

/// <summary>
/// Allow one blocked host or home path for the repo of the run that was blocked (POST /sindri/sandbox/allow; sandbox spec §3.6; desk). Edits the live profile; the owner approves it like any profile change.
/// </summary>
/// <param name="Launch">The run that was blocked.</param>
/// <param name="Kind">Network or Path; a policy denial can't be allowed.</param>
/// <param name="Target">The host, or the path, as the blocked list shows it.</param>
public sealed record AllowDenialRequest(LaunchId Launch, DenialKind Kind, string Target) : ICommand<AllowDenialResponse>;
