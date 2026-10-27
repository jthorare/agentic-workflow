using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.EvaluateQuestion;

namespace Aw.Judge.Testing;

/// <summary>Builds an EvaluateQuestionRequest with deterministic defaults: 'ask-check' scored on jev, variant 'as-is', any label source.</summary>
public sealed class EvaluateQuestionRequestBuilder
{
    private string _question = "ask-check";
    private ProviderName _provider = ProviderName.Jev;
    private string _variant = "as-is";
    private LabelSource? _labels;
    private string? _collapse;
    private string? _record;
    private string? _replay;
    private IReadOnlyList<ProviderName>? _providers;

    /// <summary>Sets the question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Question(string question)
    {
        _question = question;
        return this;
    }

    /// <summary>Sets the provider to score.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Provider(ProviderName provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>Sets the input variant.</summary>
    /// <param name="variant">The variant name.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Variant(string variant)
    {
        _variant = variant;
        return this;
    }

    /// <summary>Scores against one label source.</summary>
    /// <param name="labels">The source.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Labels(LabelSource labels)
    {
        _labels = labels;
        return this;
    }

    /// <summary>Folds every label but one into 'stalled'.</summary>
    /// <param name="collapse">The label that stays.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Collapse(string collapse)
    {
        _collapse = collapse;
        return this;
    }

    /// <summary>Records every provider answer to a file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Record(string file)
    {
        _record = file;
        return this;
    }

    /// <summary>Re-scores the answers in a file instead of calling the provider.</summary>
    /// <param name="file">The file.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Replay(string file)
    {
        _replay = file;
        return this;
    }

    /// <summary>Limits the providers to an allowlist.</summary>
    /// <param name="providers">The allowlist.</param>
    /// <returns>This builder.</returns>
    public EvaluateQuestionRequestBuilder Allow(params ProviderName[] providers)
    {
        _providers = providers;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public EvaluateQuestionRequest Build() => new(_question, _provider, _variant, _labels, _collapse, _record, _replay, _providers);
}
