using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The probe of a host that wires no records embedder: it never answers.</summary>
internal sealed class NoRecordEmbedderProbe : IRecordEmbedderProbe
{
    public string Model => "nomic-embed-text:latest";

    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(ContextErrors.SourceUnavailable.With("this host wires no records embedder"));
}
