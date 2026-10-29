using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Vectors;

namespace Aw.Context.Features.SyncSources;

/// <summary>Groups every stored record by meaning and has a model name each changed group (#1597).</summary>
internal sealed class ConceptSynthesisRun
{
    /// <summary>Builds the run over its ports.</summary>
    /// <param name="records">The record store.</param>
    /// <param name="scan">The vector scan.</param>
    /// <param name="concepts">The synthesized concepts.</param>
    /// <param name="model">The model.</param>
    /// <param name="clock">The clock.</param>
    public ConceptSynthesisRun(IRecordStore records, IVectorScan scan, IConceptSynthesisStore concepts, IModelPort model, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <summary>The most tokens one answer (a name and a sentence) may take; part of every call's estimated cost.</summary>
    internal const int AnswerTokens = 200;

    /// <summary>Runs once over the whole store.</summary>
    /// <param name="settings">The grouper's knobs and the prompt caps.</param>
    /// <param name="labeling">Its token budget is the run's cap.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run did; a failure is the report's code.</returns>
    public ValueTask<ConceptSynthesisReport> Run(ConceptSynthesisSettings settings, LabelingSettings labeling, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not written yet");
}
