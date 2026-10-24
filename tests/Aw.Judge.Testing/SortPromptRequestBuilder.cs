using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.SortPrompt;

namespace Aw.Judge.Testing;

/// <summary>Builds a SortPromptRequest with deterministic defaults: an ordinary prompt in session 'sess-1', every provider allowed.</summary>
public sealed class SortPromptRequestBuilder
{
    private string _prompt = "add a dark mode toggle to the settings page";
    private SessionId? _session = SessionId.From("sess-1");
    private IReadOnlyList<ProviderName>? _providers;

    /// <summary>Sets the prompt.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>This builder.</returns>
    public SortPromptRequestBuilder Prompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    /// <summary>Limits the providers (the --providers allowlist).</summary>
    /// <param name="providers">The allowed providers.</param>
    /// <returns>This builder.</returns>
    public SortPromptRequestBuilder Allowing(params ProviderName[] providers)
    {
        _providers = providers;
        return this;
    }

    /// <summary>Takes the session away (the hook had none).</summary>
    /// <returns>This builder.</returns>
    public SortPromptRequestBuilder WithoutSession()
    {
        _session = null;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SortPromptRequest Build() => new(_prompt, _session, _providers);
}
