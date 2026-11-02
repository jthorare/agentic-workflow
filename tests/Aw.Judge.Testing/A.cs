namespace Aw.Judge.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A GetHealthRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetHealthRequestBuilder GetHealthRequest() => new();

    /// <summary>A decision builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static DecisionBuilder Decision() => new();

    /// <summary>A brief builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static BriefBuilder Brief() => new();

    /// <summary>A SaveBriefRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SaveBriefRequestBuilder SaveBriefRequest() => new();

    /// <summary>A GetBriefRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetBriefRequestBuilder GetBriefRequest() => new();

    /// <summary>A MapBriefByNameRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static MapBriefByNameRequestBuilder MapBriefByNameRequest() => new();

    /// <summary>A MapBriefByDispatchRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static MapBriefByDispatchRequestBuilder MapBriefByDispatchRequest() => new();

    /// <summary>A SetBriefAgentIdRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SetBriefAgentIdRequestBuilder SetBriefAgentIdRequest() => new();

    /// <summary>A ExplainDecisionRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ExplainDecisionRequestBuilder ExplainDecisionRequest() => new();

    /// <summary>A UndoDecisionRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static UndoDecisionRequestBuilder UndoDecisionRequest() => new();

    /// <summary>A ApproveDecisionRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ApproveDecisionRequestBuilder ApproveDecisionRequest() => new();

    /// <summary>A GetConfigRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetConfigRequestBuilder GetConfigRequest() => new();

    /// <summary>A SetConfigRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SetConfigRequestBuilder SetConfigRequest() => new();

    /// <summary>A test question builder (wake-gate's shape over <see cref="TextInput"/>).</summary>
    /// <returns>A new builder.</returns>
    public static QuestionBuilder Question() => new();

    /// <summary>A SortPromptRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SortPromptRequestBuilder SortPromptRequest() => new();

    /// <summary>A SetPromptSortRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SetPromptSortRequestBuilder SetPromptSortRequest() => new();

    /// <summary>A CheckTraitsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static CheckTraitsRequestBuilder CheckTraitsRequest() => new();

    /// <summary>An ImportLabelsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ImportLabelsRequestBuilder ImportLabelsRequest() => new();

    /// <summary>An LabelOutcomesRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static LabelOutcomesRequestBuilder LabelOutcomesRequest() => new();

    /// <summary>An SetLabelRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SetLabelRequestBuilder SetLabelRequest() => new();

    /// <summary>An GetLabelStatusRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetLabelStatusRequestBuilder GetLabelStatusRequest() => new();

    /// <summary>An AdjudicateRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static AdjudicateRequestBuilder AdjudicateRequest() => new();

    /// <summary>An EvaluateQuestionRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static EvaluateQuestionRequestBuilder EvaluateQuestionRequest() => new();

    /// <summary>An ImportTurnsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ImportTurnsRequestBuilder ImportTurnsRequest() => new();
}
