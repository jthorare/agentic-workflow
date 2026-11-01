using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.models' (model routing spec §3, §10). Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class ModelsRules
{
    private const string Root = "context.models";
    internal const string HarnessNames = "claude-code, codex, cursor";

    private static readonly string[] _entryKeys = ["harnesses", "tier", "models"];
    private static readonly string[] _labelingKeys = ["harnesses", "tier", "models", "pick", "candidates"];
    private static readonly string[] _surfaceKeys = ["pick"];

    internal static Result<ModelsSettings> Read(JsonElement? section)
    {
        if (section is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element)
        {
            return new ModelsSettings();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return SettingsReader.Invalid(Root, "must be a map of settings");
        }

        var read = new Collected();
        foreach (var property in element.EnumerateObject())
        {
            if (read.Add(property) is { } error)
            {
                return error;
            }
        }

        return new ModelsSettings { Names = read.Names, Default = read.Default, Kinds = read.Kinds, LabelPick = read.Pick, LabelCandidates = read.Candidates, SurfacePick = read.SurfacePick };
    }

    private sealed class Collected
    {
        private readonly Dictionary<ModelKind, ModelEntry> _kinds = [];

        internal ModelNames Names { get; private set; } = ModelNames.None;

        internal ModelEntry Default { get; private set; } = new();

        internal IReadOnlyDictionary<ModelKind, ModelEntry> Kinds => _kinds;

        internal LabelPick Pick { get; private set; } = LabelPick.Off;

        internal int Candidates { get; private set; } = ModelsSettings.DefaultCandidates;

        internal LabelPick SurfacePick { get; private set; } = LabelPick.Off;

        internal Error? Add(JsonProperty property)
        {
            var path = $"{Root}.{SettingsReader.Printable(property.Name)}";
            if (string.Equals(property.Name, "names", StringComparison.Ordinal))
            {
                var (names, error) = ModelNamesRules.Read(property.Value, path);
                Names = names;
                return error;
            }

            if (string.Equals(property.Name, "default", StringComparison.Ordinal))
            {
                var (entry, error) = ReadEntry(property.Value, path, Shape.Plain);
                Default = entry.Entry;
                return error;
            }

            if (!ModelKinds.TryParse(property.Name, out var kind))
            {
                return SettingsReader.Invalid(path, $"is not a model setting this build knows (names, default, {string.Join(", ", ModelKinds.All.Select(ModelKinds.Key))})");
            }

            var (read, problem) = ReadEntry(property.Value, path, kind == ModelKind.Labeling ? Shape.Labeling : kind == ModelKind.ProductSurfaces ? Shape.PickOnly : Shape.Plain);
            _kinds[kind] = read.Entry;
            if (kind == ModelKind.Labeling)
            {
                Pick = read.Pick;
                Candidates = read.Candidates;
            }
            else if (kind == ModelKind.ProductSurfaces)
            {
                SurfacePick = read.Pick;
            }

            return problem;
        }
    }

    /// <summary>Which keys an entry may hold: the common three, those and the label pick's two, or only the product-surface pick (it has no harness list, tier or model: spec §3.2 point 3).</summary>
    private enum Shape
    {
        Plain,
        Labeling,
        PickOnly,
    }

    private readonly record struct EntryRead(ModelEntry Entry, LabelPick Pick, int Candidates);

    private static (EntryRead Value, Error? Error) ReadEntry(JsonElement element, string path, Shape shape)
    {
        var none = new EntryRead(new ModelEntry(), LabelPick.Off, ModelsSettings.DefaultCandidates);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (none, SettingsReader.Invalid(path, "must be a map of settings"));
        }

        var allowed = shape == Shape.Labeling ? _labelingKeys : shape == Shape.PickOnly ? _surfaceKeys : _entryKeys;
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                return (none, SettingsReader.Invalid($"{path}.{SettingsReader.Printable(property.Name)}", $"is not a setting of this entry ({string.Join(", ", allowed)})"));
            }
        }

        var (harnesses, harnessError) = Harnesses(element, path);
        var (tier, tierError) = Tier(element, path);
        var (models, modelsError) = ModelMap(element, path);
        var (pick, pickError) = Pick(element, path);
        var (candidates, candidatesError) = Candidates(element, path);
        var error = harnessError ?? tierError ?? modelsError ?? pickError ?? candidatesError;
        return (new EntryRead(new ModelEntry(harnesses, tier, models), pick, candidates), error);
    }

    private static (IReadOnlyList<ModelHarness>? Value, Error? Error) Harnesses(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("harnesses", out var list))
        {
            return (null, null);
        }

        var at = path + ".harnesses";
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            return (null, SettingsReader.Invalid(at, $"is a list of 1 to 3 of {HarnessNames}"));
        }

        var items = new List<ModelHarness>();
        foreach (var item in list.EnumerateArray())
        {
            var place = string.Create(CultureInfo.InvariantCulture, $"{at}[{items.Count}]");
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!ModelHarnesses.TryParse(text, out var harness))
            {
                return (null, SettingsReader.Invalid(place, UnknownHarness(text)));
            }

            if (items.Contains(harness))
            {
                return (null, SettingsReader.Invalid(place, $"\"{text}\" is listed twice"));
            }

            items.Add(harness);
        }

        return (items, null);
    }

    private static string UnknownHarness(string? text)
    {
        if (text is null)
        {
            return $"is one of {HarnessNames}";
        }

        var meant = text.Length == 0 ? null : ModelHarnesses.DefaultOrder.Select(static h => ModelHarnesses.Name(h)).FirstOrDefault(n => n.StartsWith(text, StringComparison.OrdinalIgnoreCase) || text.StartsWith(n, StringComparison.OrdinalIgnoreCase));
        return $"\"{SettingsReader.Printable(text)}\" is not one of {HarnessNames}" + (meant is null ? "" : $" (did you mean {meant}?)");
    }

    private static (ModelTier? Value, Error? Error) Tier(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("tier", out var tier))
        {
            return (null, null);
        }

        var text = tier.ValueKind == JsonValueKind.String ? tier.GetString() : null;
        return text switch
        {
            "cheap" => (ModelTier.Cheap, null),
            "strong" => (ModelTier.Strong, null),
            null => (null, SettingsReader.Invalid(path + ".tier", "is cheap or strong")),
            _ => (null, SettingsReader.Invalid(path + ".tier", $"\"{SettingsReader.Printable(text)}\" is cheap or strong")),
        };
    }

    private static (IReadOnlyDictionary<ModelHarness, string>? Value, Error? Error) ModelMap(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("models", out var map))
        {
            return (null, null);
        }

        var at = path + ".models";
        if (map.ValueKind != JsonValueKind.Object)
        {
            return (null, SettingsReader.Invalid(at, "is a map from harness to model name"));
        }

        var models = new Dictionary<ModelHarness, string>();
        foreach (var property in map.EnumerateObject())
        {
            var place = $"{at}.{SettingsReader.Printable(property.Name)}";
            if (!ModelHarnesses.TryParse(property.Name, out var harness))
            {
                return (null, SettingsReader.Invalid(place, $"is not a harness ({HarnessNames})"));
            }

            var name = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (name is null || !ModelNames.IsValid(name))
            {
                return (null, SettingsReader.Invalid(place, ModelNamesRules.NameRule));
            }

            models[harness] = name;
        }

        return (models, null);
    }

    private static (LabelPick Value, Error? Error) Pick(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("pick", out var pick))
        {
            return (LabelPick.Off, null);
        }

        var text = pick.ValueKind == JsonValueKind.String ? pick.GetString() : null;
        return text switch
        {
            "jev" => (LabelPick.Jev, null),
            "off" => (LabelPick.Off, null),
            null => (LabelPick.Off, SettingsReader.Invalid(path + ".pick", "is jev or off")),
            _ => (LabelPick.Off, SettingsReader.Invalid(path + ".pick", $"\"{SettingsReader.Printable(text)}\" is jev or off")),
        };
    }

    private static (int Value, Error? Error) Candidates(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("candidates", out var number))
        {
            return (ModelsSettings.DefaultCandidates, null);
        }

        return number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var value) && value is >= 2 and <= 5
            ? (value, null)
            : (ModelsSettings.DefaultCandidates, SettingsReader.Invalid(path + ".candidates", "is a whole number from 2 to 5"));
    }
}
