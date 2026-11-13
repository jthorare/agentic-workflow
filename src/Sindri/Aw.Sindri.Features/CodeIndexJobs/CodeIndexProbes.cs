using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>The doctor's live checks of the code index: the embedding server and model, and the lock file. Split out of <see cref="CodeIndexDoctor"/>, which reads the stored state.</summary>
/// <param name="embedder">Probed for reachability and the model digest: a live check, never a build.</param>
/// <param name="pins">Reads the lock file and compares it with the build's pins.</param>
internal sealed class CodeIndexProbes(IEmbedder embedder, ICodeIndexPins pins)
{
    /// <summary>Runs the live checks.</summary>
    /// <param name="embeddings">The profile's embeddings settings, or null when the profile names none.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>What the checks found.</returns>
    public async ValueTask<CodeIndexDoctor.Probes> Check(EmbeddingsSettings? embeddings, CancellationToken cancellationToken)
    {
        var model = embeddings is { Enabled: true }
            ? new CodeIndexDoctor.ModelProbe(Enabled: true, await embedder.Probe(embeddings, cancellationToken).ConfigureAwait(false))
            : new CodeIndexDoctor.ModelProbe(Enabled: false, Unit.Value);
        return new CodeIndexDoctor.Probes(model, pins.Check());
    }
}
