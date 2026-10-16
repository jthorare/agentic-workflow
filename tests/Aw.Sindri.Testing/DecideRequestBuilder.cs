using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Decide;

namespace Aw.Sindri.Testing;

/// <summary>Builds a DecideRequest with deterministic defaults: A.FirstDecisionId, accept, no note.</summary>
public sealed class DecideRequestBuilder
{
    private DecisionId _decision = A.FirstDecisionId;
    private DecisionOption _choice = DecisionOption.From("accept");
    private string? _note;

    /// <summary>Sets the decision.</summary>
    /// <param name="value">The decision.</param>
    /// <returns>This builder.</returns>
    public DecideRequestBuilder ForDecision(DecisionId value)
    {
        _decision = value;
        return this;
    }

    /// <summary>Sets the choice.</summary>
    /// <param name="value">The option.</param>
    /// <returns>This builder.</returns>
    public DecideRequestBuilder Choosing(DecisionOption value)
    {
        _choice = value;
        return this;
    }

    /// <summary>Sets the note.</summary>
    /// <param name="value">The note, or null.</param>
    /// <returns>This builder.</returns>
    public DecideRequestBuilder WithNote(string? value)
    {
        _note = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public DecideRequest Build() => new(_decision, _choice, _note);
}
