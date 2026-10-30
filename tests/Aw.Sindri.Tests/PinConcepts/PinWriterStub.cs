using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>A concept writer that records what it is asked and answers a fixed hash (or an error); the shared fake replaces it.</summary>
internal sealed class PinWriterStub : IProfileConceptWriter
{
    internal static ProfileHash Written { get; } = ProfileHashing.Of("written"u8);

    internal List<ConceptEdit> Edits { get; } = [];

    internal ProfileHash? IfHash { get; private set; }

    internal int Calls { get; private set; }

    internal Error? Fails { get; set; }

    public ValueTask<Result<ProfileHash>> Write(IReadOnlyList<ConceptEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken)
    {
        Calls++;
        IfHash = ifHash;
        Edits.AddRange(edits);
        return ValueTask.FromResult<Result<ProfileHash>>(Fails is { } error ? error : Written);
    }
}
