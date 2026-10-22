using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'fact:restack' (PB spec §6.1): '{ newHead?, onto, conflicts? }', the new tip after a rebase, the base moved onto, and the paths of a conflict.</summary>
internal static class RestackFact
{
    /// <summary>The fact's JSON.</summary>
    /// <param name="onto">The base branch moved onto.</param>
    /// <param name="moved">How the rebase ended.</param>
    /// <returns>The UTF-8 content.</returns>
    public static byte[] Content(string onto, RebaseOutcome moved)
    {
        ArgumentNullException.ThrowIfNull(moved);
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            if (moved.NewHead is not null)
            {
                json.WriteString("newHead", moved.NewHead);
            }

            json.WriteString("onto", onto);
            if (moved.Conflicts.Count > 0)
            {
                json.WriteStartArray("conflicts");
                foreach (var path in moved.Conflicts)
                {
                    json.WriteStringValue(path);
                }

                json.WriteEndArray();
            }

            json.WriteEndObject();
        }

        return stream.ToArray();
    }
}
