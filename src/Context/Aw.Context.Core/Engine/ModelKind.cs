using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>The kind of model call a 'context.models.&lt;kind&gt;' entry settles; one kind can serve several <see cref="ModelPurpose"/> values.</summary>
public enum ModelKind
{
    /// <summary>'labeling': naming the map's clusters.</summary>
    Labeling,

    /// <summary>'conceptNaming': the concept scan, the concept names of a map build and the concept synthesis.</summary>
    ConceptNaming,

    /// <summary>'decisionExtraction': decisions read out of records.</summary>
    DecisionExtraction,

    /// <summary>'adjudication': the eval adjudicator.</summary>
    Adjudication,

    /// <summary>'mapCleanup': the keep-or-remove pass over low-confidence proposals.</summary>
    MapCleanup,

    /// <summary>'rerank': the query's rerank arm.</summary>
    Rerank,
}
