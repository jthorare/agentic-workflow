using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The scope packet an instance's evidence step assembled in its run area (scoping spec §8.3, API9): its canonical hash and its files.</summary>
public interface IScopePacketStore
{
    /// <summary>The canonical hash ('sha256:…') of the instance's candidate packet directory, computed now.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="cancellationToken">Cancels the hash.</param>
    /// <returns>The hash; SND-ITEM-404 when the run area holds no packet; SND-SCOPING-008 when the hash tool is missing, failed or timed out.</returns>
    public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken);

    /// <summary>The text of one file of the candidate packet; the name is one of the packet's files, never a path.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text; SND-ITEM-404 when the file is not there.</returns>
    public ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken);
}
