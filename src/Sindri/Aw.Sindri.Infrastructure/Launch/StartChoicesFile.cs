using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The repo and agent of the last one-off start, in 'last-start.json' under the Sindri home (agents spec §15.4). Written beside the file and moved in, so a reader never sees half of it; a write that fails is ignored.</summary>
/// <param name="home">The Sindri home.</param>
internal sealed class StartChoicesFile(ISindriHome home) : IStartChoices
{
    internal const string FileName = "last-start.json";

    public ValueTask Remember(string repo, string agent, CancellationToken cancellationToken)
    {
        try
        {
            _ = Directory.CreateDirectory(home.Path);
            var path = Path.Combine(home.Path, FileName);
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("repo", repo);
                writer.WriteString("agent", agent);
                writer.WriteEndObject();
            }

            File.Move(temp, path, overwrite: true);
        }
        catch (IOException)
        {
            // a choice that is not kept costs the owner one pick next time
        }
        catch (UnauthorizedAccessException)
        {
            // same
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<StartChoice> Last(CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(home.Path, FileName);
            if (!File.Exists(path))
            {
                return new StartChoice(null, null);
            }

            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object ? new StartChoice(Text(root, "repo"), Text(root, "agent")) : new StartChoice(null, null);
        }
        catch (JsonException)
        {
            return new StartChoice(null, null);   // a half-written or hand-edited file is the same as none
        }
        catch (IOException)
        {
            return new StartChoice(null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new StartChoice(null, null);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
