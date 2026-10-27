using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class Launcher
{
    /// <summary>Adds what the model list says to the limits: 'no-model-list' when an installed agent's list couldn't be read (SND-MODEL-004, informational), and 'unordered: id' for a model that has no place (model-pick spec §3).</summary>
    private static string? WithModels(string? limited, IReadOnlyList<ModelFacts>? models, bool found)
    {
        if (models is null || !found)
        {
            return limited;
        }

        var notes = new List<string>();
        if (limited is not null)
        {
            notes.Add(limited);
        }

        if (models.Count == 0)
        {
            notes.Add("no-model-list");
        }

        notes.AddRange(ModelBands.Rank(models).Where(static m => m.Unordered).Select(static m => "unordered: " + m.Model.Slug));
        return notes.Count == 0 ? null : string.Join("; ", notes);
    }
}
