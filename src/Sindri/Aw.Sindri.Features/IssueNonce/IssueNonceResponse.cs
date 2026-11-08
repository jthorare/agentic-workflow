using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.IssueNonce;

/// <summary>A nonce for one command on an instance (engine spec §5.2): the command's Update id, valid 10 minutes.</summary>
/// <param name="Instance">The instance it is for.</param>
/// <param name="Nonce">The nonce.</param>
public sealed record IssueNonceResponse(InstanceAddress Instance, CommandNonce Nonce);
