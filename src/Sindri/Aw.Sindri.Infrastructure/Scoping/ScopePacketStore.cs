using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>
/// The candidate scope packet in the instance's run area, $AW_SINDRI_HOME/workflow/runs/&lt;instance&gt;/scope-packet/ (scoping spec §8.3),
/// hashed with aw-packets hash --kind scoping (AW_PACKETS_BIN, else aw-packets on the PATH) and read file by file.
/// </summary>
/// <param name="settings">Sindri's home.</param>
/// <param name="packets">Hashes the packet (aw-packets).</param>
internal sealed class ScopePacketStore(SindriSettings settings, IPackets packets) : IScopePacketStore
{
    private const long MaxBytes = 4 * 1024 * 1024;

    public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        var directory = PacketDirectory(instance);
        return Directory.Exists(directory)
            ? packets.Hash(new PacketRef(PacketKind.Scoping, directory), cancellationToken)
            : ValueTask.FromResult<Result<string>>(NotThere(instance));
    }

    public async ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(PacketDirectory(instance), name);
        if (!string.Equals(Path.GetFileName(path), name, StringComparison.Ordinal) || !File.Exists(path) || new FileInfo(path).Length > MaxBytes)
        {
            return NotThere(instance);
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private string PacketDirectory(WorkflowInstanceId instance) => Path.Combine(RunAreaDirectory.Of(settings.Home, instance), "scope-packet");

    private static Error NotThere(WorkflowInstanceId instance) => SindriErrors.ItemNotFound.With($"No scope packet in the run area of workflow instance {instance.Value}.");
}
