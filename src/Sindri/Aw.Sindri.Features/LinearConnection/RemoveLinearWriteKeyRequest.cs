using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Removes the Linear write key's pointer from the live profile. Owner only.</summary>
internal sealed record RemoveLinearWriteKeyRequest : ICommand<RemoveLinearWriteKeyResponse>;
