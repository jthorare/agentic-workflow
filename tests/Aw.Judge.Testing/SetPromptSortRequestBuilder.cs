using Aw.Judge.Features.SetPromptSort;

namespace Aw.Judge.Testing;

/// <summary>Builds a SetPromptSortRequest with deterministic defaults: promptSort.enabled to true.</summary>
public sealed class SetPromptSortRequestBuilder
{
    private SetPromptSortRequest _request = new(Enabled: true);

    /// <summary>Sets promptSort.enabled.</summary>
    /// <param name="enabled">The flag.</param>
    /// <returns>This builder.</returns>
    public SetPromptSortRequestBuilder Enabled(bool enabled)
    {
        _request = new SetPromptSortRequest(Enabled: enabled);
        return this;
    }

    /// <summary>Switches one scaffold.</summary>
    /// <param name="scaffold">The scaffold id.</param>
    /// <param name="on">Whether it fires.</param>
    /// <returns>This builder.</returns>
    public SetPromptSortRequestBuilder Scaffold(string scaffold, bool on)
    {
        _request = new SetPromptSortRequest(Scaffold: new ScaffoldSwitch(scaffold, on));
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SetPromptSortRequest Build() => _request;
}
