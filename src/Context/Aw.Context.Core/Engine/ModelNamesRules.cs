using System.Text.Json;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads 'context.models.names': a cheap and a strong model name for each harness.</summary>
internal static class ModelNamesRules
{
    internal const string NameRule = "a model name is letters, digits and . _ : / @ + - , not starting with - (1 to 100 characters, no '..')";

    internal static (ModelNames Value, Error? Error) Read(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (ModelNames.None, SettingsReader.Invalid(path, "is a map from harness to its cheap and strong model names"));
        }

        var names = new Dictionary<ModelHarness, CliModels>();
        foreach (var property in element.EnumerateObject())
        {
            var place = $"{path}.{SettingsReader.Printable(property.Name)}";
            if (!ModelHarnesses.TryParse(property.Name, out var harness))
            {
                return (ModelNames.None, SettingsReader.Invalid(place, $"is not a harness ({ModelsRules.HarnessNames})"));
            }

            var (cli, error) = ReadCli(property.Value, place);
            if (error is not null)
            {
                return (ModelNames.None, error);
            }

            names[harness] = cli;
        }

        CliModels Of(ModelHarness harness) => names.GetValueOrDefault(harness, new CliModels(Cheap: null, Strong: null));
        return (new ModelNames(Of(ModelHarness.ClaudeCode), Of(ModelHarness.Codex), Of(ModelHarness.Cursor)), null);
    }

    private static (CliModels Value, Error? Error) ReadCli(JsonElement element, string path)
    {
        var none = new CliModels(Cheap: null, Strong: null);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (none, SettingsReader.Invalid(path, "is a map with cheap and strong model names"));
        }

        string? cheap = null;
        string? strong = null;
        foreach (var property in element.EnumerateObject())
        {
            var place = $"{path}.{SettingsReader.Printable(property.Name)}";
            if (!string.Equals(property.Name, "cheap", StringComparison.Ordinal) && !string.Equals(property.Name, "strong", StringComparison.Ordinal))
            {
                return (none, SettingsReader.Invalid(place, "is cheap or strong"));
            }

            var name = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (name is null || !ModelNames.IsValid(name))
            {
                return (none, SettingsReader.Invalid(place, NameRule));
            }

            if (string.Equals(property.Name, "cheap", StringComparison.Ordinal))
            {
                cheap = name;
            }
            else
            {
                strong = name;
            }
        }

        return (new CliModels(cheap, strong), null);
    }
}
