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
}
