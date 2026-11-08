using Aw.Kernel;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features;

/// <summary>The ledger side of a build that registers no ledger adapter: no event is mirrored.</summary>
internal sealed class NoMirroredSequences : IMirroredSequences
{
    public ValueTask<Result<IReadOnlyDictionary<string, long>>> Of(InstanceAddress address, string run, IReadOnlyCollection<string> activities, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>(StringComparer.Ordinal)));
}
