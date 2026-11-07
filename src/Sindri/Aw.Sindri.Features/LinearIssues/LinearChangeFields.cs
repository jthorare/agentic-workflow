using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The fields of an edit (Linear hub spec 2026-11-04 §3): reads the request's field maps into plain comparable values, reads the same values off Linear's live issue, and says whether two agree.
/// A value is a string, an int, a sorted array of label ids, or null (a field that is cleared). An absent description and an empty one are the same value.
/// </summary>
internal static class LinearChangeFields
{
    internal const string Title = "title";
    internal const string Description = "description";
    internal const string StateId = "stateId";
    internal const string AssigneeId = "assigneeId";
    internal const string Priority = "priority";
    internal const string ProjectId = "projectId";
    internal const string CycleId = "cycleId";
    internal const string LabelIds = "labelIds";
    internal const string ParentKey = "parentKey";

    private static readonly string[] _order = [Title, Description, StateId, AssigneeId, Priority, ProjectId, CycleId, LabelIds, ParentKey];

    /// <summary>Reads one field map of a request.</summary>
    /// <param name="fields">Field name to value.</param>
    /// <param name="part">'set' or 'opened', for the message.</param>
    /// <returns>The values in the order the fields are listed in; SND-CLI-003 for a field that isn't one, a value of the wrong type or a priority outside 0 to 4.</returns>
    internal static Result<IReadOnlyList<KeyValuePair<string, object?>>> Read(IReadOnlyDictionary<string, JsonElement> fields, string part)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Keys.FirstOrDefault(static k => !_order.Contains(k, StringComparer.Ordinal)) is { } unknown)
        {
            return SindriErrors.InvalidInput.With($"{part}.{unknown} isn't a Linear issue field (fix: use title, description, stateId, assigneeId, priority, projectId, cycleId, labelIds or parentKey)");
        }

        var values = new List<KeyValuePair<string, object?>>();
        foreach (var name in _order.Where(fields.ContainsKey))
        {
            var value = Value(name, fields[name]);
            if (value.IsError)
            {
                return SindriErrors.InvalidInput.With($"{part}.{name} {value.Error.Message}");
            }

            values.Add(new KeyValuePair<string, object?>(name, value.Value));
        }

        return values;
    }

    /// <summary>The value of <paramref name="name"/> on Linear's live issue.</summary>
    /// <param name="live">The issue as Linear has it.</param>
    /// <param name="name">A field name other than parentKey.</param>
    /// <returns>The value, in the form <see cref="Read"/> gives.</returns>
    internal static object? Live(LinearIssueView live, string name)
    {
        ArgumentNullException.ThrowIfNull(live);
        return name switch
        {
            Title => live.Title,
            Description => live.Description ?? string.Empty,
            StateId => live.StateId.Value,
            AssigneeId => live.AssigneeId?.Value,
            Priority => live.Priority,
            ProjectId => live.ProjectId?.Value,
            CycleId => live.CycleId?.Value,
            LabelIds => Sorted(live.LabelIds.Select(static l => l.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such comparable field"),
        };
    }

    /// <summary>Whether two values of one field agree.</summary>
    /// <param name="left">A value.</param>
    /// <param name="right">A value.</param>
    /// <returns>True when they are the same.</returns>
    internal static bool Same(object? left, object? right) =>
        left is string[] a && right is string[] b ? a.AsSpan().SequenceEqual(b) : Equals(left, right);

    /// <summary>A value as JSON.</summary>
    /// <param name="value">A value in the form <see cref="Read"/> gives.</param>
    /// <returns>The JSON.</returns>
    internal static JsonElement Json(object? value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case int number:
                    writer.WriteNumberValue(number);
                    break;
                case string[] ids:
                    writer.WriteStartArray();
                    foreach (var id in ids)
                    {
                        writer.WriteStringValue(id);
                    }

                    writer.WriteEndArray();
                    break;
                default:
                    writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        using var parsed = JsonDocument.Parse(buffer.WrittenMemory);
        return parsed.RootElement.Clone();
    }

    /// <summary>A string as JSON.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The JSON string.</returns>
    internal static JsonElement Text(string text) => Json(text);

    /// <summary>Builds the change Context sends.</summary>
    /// <param name="set">The new values.</param>
    /// <param name="parent">The parent issue's Linear id when parentKey is set and not null.</param>
    /// <returns>The change.</returns>
    internal static LinearIssueChange ToChange(IReadOnlyList<KeyValuePair<string, object?>> set, ExternalId? parent)
    {
        ArgumentNullException.ThrowIfNull(set);
        var change = new LinearIssueChange();
        foreach (var (name, value) in set)
        {
            change = name switch
            {
                Title => change with { Title = LinearFieldChange.Set(value as string ?? string.Empty) },
                Description => change with { Description = LinearFieldChange.Set<string?>(value is string { Length: > 0 } text ? text : null) },
                StateId => change with { StateId = LinearFieldChange.Set(ExternalId.From(value as string ?? string.Empty)) },
                AssigneeId => change with { AssigneeId = LinearFieldChange.Set<ExternalId?>(Id(value)) },
                Priority => change with { Priority = LinearFieldChange.Set(value is int level ? level : 0) },
                ProjectId => change with { ProjectId = LinearFieldChange.Set<ExternalId?>(Id(value)) },
                CycleId => change with { CycleId = LinearFieldChange.Set<ExternalId?>(Id(value)) },
                LabelIds => change with { LabelIds = LinearFieldChange.Set<IReadOnlyList<ExternalId>>([.. (value as string[] ?? []).Select(ExternalId.From)]) },
                _ => change with { ParentId = LinearFieldChange.Set<ExternalId?>(parent) },
            };
        }

        return change;
    }

    private static Error Bad(string reason) => SindriErrors.InvalidInput.With(reason);

    private static ExternalId? Id(object? value) => value is string text ? ExternalId.From(text) : null;

    private static string[] Sorted(IEnumerable<string> ids) => [.. ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static Result<object?> Value(string name, JsonElement element)
    {
        var isNull = element.ValueKind == JsonValueKind.Null;
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        return name switch
        {
            Title or StateId => text is null ? Bad("must be text") : Ok(text),
            Description => text is not null ? Ok(text) : isNull ? Ok(string.Empty) : Bad("must be text or null"),
            Priority => Level(element),
            LabelIds => Labels(element),
            _ => Reference(name, text, isNull),
        };
    }

    private static Result<object?> Reference(string name, string? text, bool isNull) =>
        text is null && !isNull ? Bad(name == ParentKey ? "must be an issue key or null" : "must be an id or null")
        : Ok(name == ParentKey ? text?.Trim().ToUpperInvariant() : text);

    private static Result<object?> Level(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var level) && level is >= 0 and <= 4 ? Ok(level) : Bad("must be a number from 0 to 4");

    private static Result<object?> Ok(object? value) => Results.Success(value);

    private static Result<object?> Labels(JsonElement element)
    {
        var ids = element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().Select(static e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList() : null;
        return ids is null || ids.Exists(static id => id is null) ? Bad("must be a list of label ids") : Ok(Sorted(ids.OfType<string>()));
    }
}
