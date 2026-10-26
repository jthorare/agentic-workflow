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
}
