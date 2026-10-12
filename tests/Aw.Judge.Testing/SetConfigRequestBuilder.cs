using Aw.Judge.Features.SetConfig;

namespace Aw.Judge.Testing;

/// <summary>Builds a SetConfigRequest with deterministic defaults: wake-gate's threshold to 0.85.</summary>
public sealed class SetConfigRequestBuilder
{
    private SetConfigRequest _request = new("wake-gate", Threshold: 0.85);

    /// <summary>Sets a question's threshold.</summary>
    /// <param name="question">The question.</param>
    /// <param name="threshold">The threshold.</param>
    /// <returns>This builder.</returns>
    public SetConfigRequestBuilder Threshold(string question, double threshold)
    {
        _request = new SetConfigRequest(question, Threshold: threshold);
        return this;
    }

    /// <summary>Sets a question's enabled flag.</summary>
    /// <param name="question">The question.</param>
    /// <param name="enabled">The flag.</param>
    /// <returns>This builder.</returns>
    public SetConfigRequestBuilder Enabled(string question, bool enabled)
    {
        _request = new SetConfigRequest(question, Enabled: enabled);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SetConfigRequest Build() => _request;
}
