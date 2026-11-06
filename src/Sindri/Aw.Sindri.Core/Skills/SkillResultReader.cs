using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Skills;

/// <summary>The pure decision behind the result read at each turn end (skill flows spec §3.3).</summary>
public static partial class SkillResultReader
{
    private static readonly IReadOnlyDictionary<string, string> _noFacts = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Decides what the turn end means. The order matters: a file older than the launch's last prompt is a turn still working; no file, or one from before the launch, is a stop without a result; a file not newer than the last turn end handled is the same answer; then the content: not JSON, another skill's, a question ('waiting', which comes before the outcome map), an unmapped verdict, a missing or bad output, else the finished result.</summary>
    /// <param name="input">What the reader looks at.</param>
    /// <returns>The decision.</returns>
    public static SkillResultDecision Decide(SkillResultInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.LastPrompt is { } prompt && (input.File is null || input.File.Modified <= prompt))
        {
            return Of(SkillResultKind.Working);   // why: a prompt after the file was written means a new turn runs (a background subagent's notification); the file says nothing new
        }

        if (input.File is not { } file || file.Modified < input.LaunchStarted)
        {
            return Of(SkillResultKind.OwnerWaiting, SkillWaitReason.NoResult);
        }

        if (input.LastHandledTurnEnd is { } handled && file.Modified <= handled)
        {
            return Of(SkillResultKind.StillWaiting);
        }

        try
        {
            using var document = JsonDocument.Parse(file.Bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Read(document.RootElement, input)
                : Of(SkillResultKind.OwnerWaiting, SkillWaitReason.Unreadable, SindriErrors.SkillResultUnreadable, "the result is not a JSON object");
        }
        catch (JsonException e)
        {
            return Of(SkillResultKind.OwnerWaiting, SkillWaitReason.Unreadable, SindriErrors.SkillResultUnreadable, e.Message);
        }
    }

    private static SkillResultDecision Read(JsonElement root, SkillResultInput input)
    {
        if (Text(root, "skill") is { } other && !string.Equals(other, input.Skill, StringComparison.Ordinal))
        {
            return Of(SkillResultKind.OwnerWaiting, SkillWaitReason.Unreadable, SindriErrors.SkillResultWrongSkill, other);
        }

        if (Text(root, "waiting") is { Length: > 0 } question)
        {
            return Of(SkillResultKind.OwnerWaiting, SkillWaitReason.Question) with { Text = question };
        }

        var (outcome, unmapped) = OutcomeOf(root, input);
        if (unmapped is not null)
        {
            return unmapped;
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var output in input.Block.Outputs)
        {
            var (value, failed) = Output(root, input, output);
            if (failed is not null)
            {
                return failed;
            }

            facts[SkillDescriptors.FactKeyOf(input.Block.Name, output.Key).Value] = value;
        }

        return new SkillResultDecision(SkillResultKind.Complete, Reason: null, outcome, facts, Text: null, Error: null, Detail: null);
    }

    private static (string Value, SkillResultDecision? Failure) OutcomeOf(JsonElement root, SkillResultInput input)
    {
        var outcomes = input.Block.Outcomes;
        if (outcomes.From is not { } key)
        {
            return (outcomes.Success, null);
        }

        var said = Text(root, key);
        return said is not null && outcomes.Map.TryGetValue(said, out var mapped)
            ? (mapped, null)
            : (string.Empty, Of(SkillResultKind.StepFailed, reason: null, SindriErrors.SkillVerdictUnmapped, $"{input.Skill} said '{said ?? "nothing"}', which its block.yaml doesn't map"));
    }

    private static (string Value, SkillResultDecision? Failure) Output(JsonElement root, SkillResultInput input, SkillOutput output)
    {
        string? value;
        var artifact = ArtifactName.Match(output.From);
        if (artifact.Success)
        {
            var wanted = artifact.Groups["name"].Value;
            var names = Artifacts(root);
            value = names.FirstOrDefault(p => string.Equals(FileName(p), wanted, StringComparison.Ordinal));
            if (value is null)
            {
                return Fail($"{input.Skill}'s result has no artifact '{wanted}' (it has: {string.Join(", ", names.Select(FileName))})");
            }
        }
        else
        {
            value = Text(root, output.From);
            if (value is null)
            {
                return Fail($"{input.Skill}'s result has no '{output.From}'");
            }
        }

        return output.Type == SkillValueType.Path && PathProblem(value, input.PathExists) is { } problem
            ? Fail($"{input.Skill}'s output '{output.Key}' {problem}")
            : (value, null);
    }

    private static string? PathProblem(string path, Func<string, bool> exists) => path switch
    {
        _ when path.Contains('\n', StringComparison.Ordinal) || path.Contains('\r', StringComparison.Ordinal) => "holds a newline",
        _ when !path.StartsWith('/') => $"is not an absolute path ('{path}')",
        _ when !exists(path) => $"names '{path}', which is not on disk",
        _ => null,
    };

    private static (string Value, SkillResultDecision? Failure) Fail(string detail) =>
        (string.Empty, Of(SkillResultKind.StepFailed, reason: null, SindriErrors.SkillOutputInvalid, detail));

    // A string, or a scalar read as the text it prints (a PR number); null for an absent key, null, an object or an array.
    private static string? Text(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False ? value.GetRawText() : null;
    }

    private static List<string> Artifacts(JsonElement root) =>
        root.TryGetProperty("artifacts", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(static a => a.ValueKind == JsonValueKind.String).Select(static a => a.GetString() ?? string.Empty)]
            : [];

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static SkillResultDecision Of(SkillResultKind kind, SkillWaitReason? reason = null, ErrorDefinition? error = null, string? detail = null) =>
        new(kind, reason, Outcome: null, _noFacts, Text: null, error, detail);

    [GeneratedRegex("^artifacts\\[(?<name>.+)\\]\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArtifactName { get; }
}
