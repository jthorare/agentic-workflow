using System.Text.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The repo and agent of the last one-off start, in 'last-start.json' under the Sindri home (agents spec §15.4). Written beside the file and moved in, so a reader never sees half of it; a write that fails is ignored.</summary>
/// <param name="home">The Sindri home.</param>
internal sealed class StartChoicesFile(ISindriHome home) : IStartChoices
{
    internal const string FileName = "last-start.json";

    public async ValueTask Remember(string repo, string agent, CancellationToken cancellationToken)
    {
        try
        {
            _ = Directory.CreateDirectory(home.Path);
            var path = Path.Combine(home.Path, FileName);
            var temp = path + "." + Path.GetRandomFileName() + ".tmp";   // why: two starts at once never share a temp file (#3466)
            var stream = new FileStream(temp, FileMode.Create, FileAccess.Write);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new Utf8JsonWriter(stream);
                await using (writer.ConfigureAwait(false))
                {
                    writer.WriteStartObject();
                    writer.WriteString("repo", repo);
                    writer.WriteString("agent", agent);
                    writer.WriteEndObject();
                }
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
    }

    public async ValueTask<StartChoice> Last(CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(home.Path, FileName);
            if (!File.Exists(path))
            {
                return new StartChoice(Repo: null, Agent: null);
            }

            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object ? new StartChoice(Text(root, "repo"), Text(root, "agent")) : new StartChoice(Repo: null, Agent: null);
        }
        catch (JsonException)
        {
            return new StartChoice(Repo: null, Agent: null);   // a half-written or hand-edited file is the same as none
        }
        catch (IOException)
        {
            return new StartChoice(Repo: null, Agent: null);
        }
        catch (UnauthorizedAccessException)
        {
            return new StartChoice(Repo: null, Agent: null);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
