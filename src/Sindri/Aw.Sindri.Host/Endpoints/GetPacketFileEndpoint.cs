using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/packets/{hash}/files/{name} (scoping spec API9; query): one file of the instance's candidate scope packet by
/// its hash. JSON files are served as JSON, markdown and JSON lines as text; a session caller's text is inside the pack fence. The hash is URL-escaped
/// ('sha256%3A…' or as is); the name is one the packet lists, never a path.
/// </summary>
internal static class GetPacketFileEndpoint
{
    internal static RouteGroupBuilder MapGetPacketFile(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/packets/{hash}/files/{name}", static async Task<IResult> (string instanceId, string hash, string name, IService<GetPacketFileRequest, PacketFile> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var read = await service.Handle(new GetPacketFileRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId)), hash, name), ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.ToHttpResult();
            }

            var file = callers.Current?.Class == CallerClass.Session ? PacketFileFencing.Fence(read.Value) : read.Value;
            return TypedResults.Text(file.Text, string.Equals(file.Format, "json", StringComparison.Ordinal) ? "application/json" : "text/plain", System.Text.Encoding.UTF8);
        });
        return group;
    }
}
