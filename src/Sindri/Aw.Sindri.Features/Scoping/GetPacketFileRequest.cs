using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One file of a packet the instance made (scoping spec API9; GET /workflow/instances/{id}/packets/{hash}/files/{name}; query).</summary>
/// <param name="Instance">The instance id.</param>
/// <param name="Hash">The packet's canonical hash: the instance's current candidate.</param>
/// <param name="Name">The file's name, one the scope packet lists (never a path).</param>
public sealed record GetPacketFileRequest(WorkflowInstanceId Instance, string Hash, string Name) : IQuery<PacketFile>;
