using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;

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
    /// Whether Codex runs its own default model: no model flag at all, so neither <see cref="Model"/> nor the provider's small default is
    /// passed (Sindri's built-in codex name when codex's list lacks it, issue 3389); false passes a model as before. Only the Codex provider reads it.
    /// </summary>
    public bool OwnDefaultModel { get; init; }

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

    /// <summary>
    /// Whether Jev may settle this question although it asks for reasons Jev cannot give (visual-critique: Jev answers a page that looks right in
    /// one call and the CLIs say what is wrong); false keeps Jev out of every question that asks for reasons.
    /// </summary>
    public bool JevMaySettle { get; init; }

    /// <summary>The images the question sends, read once (empty for a text question): every provider that sends images gets these bytes.</summary>
    public IReadOnlyList<ImageAttachment> Images { get; init; } = [];

    /// <summary>
    /// Whether a bad or unusable image is dropped and the call goes on (a hook's call), instead of the call being refused; Jev then drops what it
    /// can't take and answers with the rest, as text when none are left.
    /// </summary>
    public bool ImagesBestEffort { get; init; }

    /// <summary>Where the agent CLIs open <see cref="Images"/> from; null when there are none or the caller has no way to write them.</summary>
    public ImageScope? ImageScope { get; init; }
}
