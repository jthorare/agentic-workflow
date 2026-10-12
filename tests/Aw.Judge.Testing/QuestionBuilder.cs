using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;

namespace Aw.Judge.Testing;

/// <summary>A test question over <see cref="TextInput"/>: wake-gate's shape (send, batch, drop; 0.7; message-meta; 5 s).</summary>
public sealed class QuestionBuilder : Builder<QuestionBuilder, QuestionDefinition<TextInput>>
{
    private QuestionDefinition<TextInput> _question = new()
    {
        Name = "wake-gate",
        Outputs = ["send", "batch", "drop"],
        Prompt = input => $"Classify: {input.Text}",
        Threshold = 0.7,
        ContentClass = ContentClass.MessageMeta,
        TimeBudget = TimeSpan.FromSeconds(5),
    };

    /// <summary>Sets the threshold.</summary>
    /// <param name="threshold">The threshold.</param>
    /// <returns>This builder.</returns>
    public QuestionBuilder WithThreshold(double threshold)
    {
        _question = _question with { Threshold = threshold };
        return this;
    }

    /// <summary>Sets the pre-rules.</summary>
    /// <param name="rules">The rules.</param>
    /// <returns>This builder.</returns>
    public QuestionBuilder WithPreRules(Func<TextInput, string?> rules)
    {
        _question = _question with { PreRules = rules };
        return this;
    }

    /// <summary>Sets the fallback rules.</summary>
    /// <param name="rules">The rules.</param>
    /// <returns>This builder.</returns>
    public QuestionBuilder WithFallbackRules(Func<TextInput, string?> rules)
    {
        _question = _question with { FallbackRules = rules };
        return this;
    }

    /// <summary>Declares an extra response field (an integer).</summary>
    /// <param name="name">The field's name.</param>
    /// <returns>This builder.</returns>
    public QuestionBuilder WithExtraProperty(string name)
    {
        _question = _question with
        {
            ExtraProperties = new Dictionary<string, System.Text.Json.Nodes.JsonObject>(StringComparer.Ordinal) { [name] = new() { ["type"] = "integer" } },
        };
        return this;
    }

    /// <summary>Sets the input check.</summary>
    /// <param name="problem">The check.</param>
    /// <returns>This builder.</returns>
    public QuestionBuilder WithInputProblem(Func<TextInput, string?> problem)
    {
        _question = _question with { InputProblem = problem };
        return this;
    }

    /// <inheritdoc />
    public override QuestionDefinition<TextInput> Build() => _question;
}
