using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'jev' block: asks Jev (the judge's HTTP API) choice, score or noul questions about a text.</summary>
public sealed class JevBlock(IJevPort jev) : IGraphBlock
{
    private const int MaxChoices = 255;
    private const int MaxLevels = 10;

    private static readonly string[] _reserved = ["model", "usage"];

    /// <inheritdoc />
    public string Type => "jev";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("connection", BlockFieldType.Text, Required: false),
        new("model", BlockFieldType.Text, Required: false),
        new("text", BlockFieldType.Text),
        new("questions", BlockFieldType.MapList),
    ]);

    /// <inheritdoc />
    public string Example => """{"text":"{{search.facts}}","questions":[{"name":"mood","type":"choice","instructions":"How does the writer feel?","criteria":{"calm":"relaxed","angry":"upset"}}]}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["model"] = "string", ["usage"] = "object" };
        if (config["questions"] is JsonArray list)
        {
            foreach (var name in list.Select(static q => BlockConfig.Text((q as JsonObject)?["name"])).OfType<string>().Where(n => !_reserved.Contains(n, StringComparer.Ordinal)))
            {
                fields[name] = "object";
            }
        }

        return new BlockOutputs(fields);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (config["questions"] is not JsonArray list)
        {
            return problems;
        }

        if (list.Count == 0)
        {
            problems.Add(("questions", "needs at least one question; add one"));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            if (Question(list[i]) is not { } question)
            {
                problems.Add(("questions", $"question {(i + 1).ToString(CultureInfo.InvariantCulture)} needs a 'name', a 'type' (choice, score or noul), 'instructions' and, for choice and score, 'criteria'; fix it"));
            }
            else if (_reserved.Contains(question.Name, StringComparer.Ordinal) || !seen.Add(question.Name))
            {
                problems.Add(("questions", $"question {(i + 1).ToString(CultureInfo.InvariantCulture)} name '{question.Name}' is reserved or used twice; rename it"));
            }
        }

        return problems;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (BlockConfig.Text(context.Config["text"]) is not { } text)
        {
            return SindriErrors.GraphBlockFailed.With("jev needs a non-empty 'text'");
        }

        if (CheckConfig(context.Config).Count > 0 || context.Config["questions"] is not JsonArray list)
        {
            return SindriErrors.GraphBlockFailed.With("jev needs 'questions' that each fit: " + string.Join("; ", CheckConfig(context.Config).Select(static p => $"{p.Field} {p.Problem}")));
        }

        var questions = list.Select(Question).OfType<JevQuestion>().ToList();
        var asked = await jev.Ask(new JevCall(BlockConfig.Text(context.Config["model"]), text, questions), cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (!asked.Value.Answers.TryGetValue(question.Name, out var answer))
            {
                return SindriErrors.GraphBlockFailed.With($"jev: unparseable-answer (no answer for '{question.Name}')");
            }

            outputs[question.Name] = Answer(answer);
        }

        outputs["model"] = asked.Value.Model;
        outputs["usage"] = new JsonObject { ["input_tokens"] = asked.Value.InputTokens, ["output_tokens"] = asked.Value.OutputTokens };
        return new BlockResult(outputs);
    }

    private static JevQuestion? Question(JsonNode? node)
    {
        if (node is not JsonObject o || BlockConfig.Text(o["name"]) is not { } name || BlockConfig.Text(o["type"]) is not { } type || BlockConfig.Text(o["instructions"]) is not { } instructions)
        {
            return null;
        }

        var criteria = (o["criteria"] as JsonObject)?.ToDictionary(static p => p.Key, static p => BlockConfig.Text(p.Value) ?? string.Empty, StringComparer.Ordinal);
        return type switch
        {
            "noul" when criteria is null => new JevQuestion(name, type, instructions, Criteria: null),
            "choice" when criteria is { Count: > 0 and <= MaxChoices } => new JevQuestion(name, type, instructions, criteria),
            "score" when criteria is { Count: >= 2 and <= MaxLevels } => new JevQuestion(name, type, instructions, criteria),
            _ => null,
        };
    }

    private static JsonObject Answer(JevAnswerValue answer)
    {
        var json = new JsonObject { ["type"] = answer.Type };
        if (answer.Choice is not null)
        {
            json["choice"] = answer.Choice;
        }

        if (answer.Score is { } score)
        {
            json["score"] = score;
        }

        if (answer.Noul is { } noul)
        {
            json["noul"] = noul;
        }

        if (answer.Confidence is { } confidence)
        {
            json["confidence"] = confidence;
        }

        if (answer.Probabilities is { } probabilities)
        {
            json["probabilities"] = new JsonObject(probabilities.Select(static p => KeyValuePair.Create<string, JsonNode?>(p.Key, p.Value)));
        }

        return json;
    }
}
