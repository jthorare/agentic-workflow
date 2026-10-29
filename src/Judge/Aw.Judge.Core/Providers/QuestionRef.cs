using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Providers;

/// <summary>What a provider needs of a question: its name, the allowed outputs and the prompt built for this input.</summary>
public sealed record QuestionRef
{
    /// <summary>The question name, e.g. 'wake-gate'.</summary>
    public required string Name { get; init; }

    /// <summary>The allowed decisions; anything else is an 'out-of-enum' failure.</summary>
    public required IReadOnlyList<string> Outputs { get; init; }

    /// <summary>The prompt built from the input.</summary>
    public required string Prompt { get; init; }

    /// <summary>The input's content class.</summary>
    public required ContentClass ContentClass { get; init; }

    /// <summary>Response fields beyond the decision (a JSON Schema fragment each, e.g. {"type":"integer"}); they ride back as extra.</summary>
    public IReadOnlyDictionary<string, JsonObject>? ExtraProperties { get; init; }

    /// <summary>Whether the answer must state a confidence (choose: the agent CLIs' schema then requires one, so their answers can fall below the threshold).</summary>
    public bool AskConfidence { get; init; }

    /// <summary>What each output means, for providers that take criteria.</summary>
    public IReadOnlyDictionary<string, string>? Criteria { get; init; }

    /// <summary>The model to run instead of the provider's own small default (an agent CLI's model name); null keeps the default.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// The prompt without the input's text, for a provider that is sent the input separately (Jev's state); null when the prompt is
    /// all there is. Choose sets it: its facts ride in the state once, the agent CLIs get them in <see cref="Prompt"/>.
    /// </summary>
    public string? PromptWithoutInput { get; init; }

    /// <summary>The cap on the state and the instructions Jev is sent, in characters; null keeps config.json's providers.inputCapChars, else 160,000 (ProvidersConfig.DefaultInputCapChars).</summary>
    public int? InputCapChars { get; init; }

    /// <summary>
    /// Whether an agent CLI's child inherits only the shared list of variables plus the ones its own login needs, so no token of the
    /// caller's (GH_TOKEN, AW_FORGE_TOKEN, cloud keys) reaches a model that reads repo-controlled text; false inherits everything.
    /// </summary>
    public bool MinimalEnvironment { get; init; }
}
