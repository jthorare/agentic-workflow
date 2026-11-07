using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>
/// <see cref="IFixTaskFiles"/> on disk: <c language="text">&lt;home&gt;/fix-tasks/&lt;sha256 of the head&gt;.json</c> (0600, written atomically), one per PR head, holding
/// <c language="text">{"version":1,"head":"acme/api:feat/x","tasks":[{"item":"ITM-…","pr":2071}]}</c> with the tasks ordered by item id. A file left with no task is deleted.
/// The sindri-tasks.sh hook reads these files and writes only its own <c language="text">seen/</c> folder beside them.
/// </summary>
/// <param name="home">Sindri's state folder.</param>
internal sealed class FixTaskFiles(ISindriHome home) : IFixTaskFiles
{
    // why: Add and Remove are read-modify-write on the same files, and one serve process is their only writer
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<Result<Unit>> Add(string head, string item, int pr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(item);
        return await Locked(() =>
        {
            var path = Path.Combine(FolderPath(), FixHead.FileName(head));
            var tasks = Tasks(path).Where(t => !string.Equals(t.Item, item, StringComparison.Ordinal)).Append((item, pr));
            Save(path, head, tasks);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<Unit>> Remove(string item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return await Locked(() =>
        {
            if (!Directory.Exists(FolderPath()))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(FolderPath(), "*.json"))
            {
                var tasks = Tasks(path);
                if (tasks.Exists(t => string.Equals(t.Item, item, StringComparison.Ordinal)))
                {
                    Save(path, HeadOf(path), tasks.Where(t => !string.Equals(t.Item, item, StringComparison.Ordinal)));
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private string FolderPath() => Path.Combine(home.Path, "fix-tasks");

    private static async ValueTask<Result<Unit>> Locked(Action work, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            work();
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ServiceFailed.With("the fix-task files can't be written: " + e.Message);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private static List<(string Item, int Pr)> Tasks(string path)
    {
        if (Read(path)?["tasks"] is not JsonArray array)
        {
            return [];
        }

        List<(string Item, int Pr)> tasks = [];
        foreach (var task in array.OfType<JsonObject>())
        {
            if (task["item"]?.GetValue<string>() is { } item && task["pr"]?.GetValue<int>() is { } pr)
            {
                tasks.Add((item, pr));
            }
        }

        return tasks;
    }

    private static string HeadOf(string path) => Read(path)?["head"]?.GetValue<string>() ?? string.Empty;

    private static JsonObject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private void Save(string path, string head, IEnumerable<(string Item, int Pr)> tasks)
    {
        var ordered = tasks.OrderBy(static t => t.Item, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0)
        {
            File.Delete(path);
            return;
        }

        OwnerOnly.EnsureDirectory(FolderPath());
        var body = new JsonObject
        {
            ["version"] = 1,
            ["head"] = head,
            ["tasks"] = new JsonArray([.. ordered.Select(static t => (JsonNode)new JsonObject { ["item"] = t.Item, ["pr"] = t.Pr })]),
        };
        OwnerOnly.WriteAtomically(path, Encoding.UTF8.GetBytes(body.ToJsonString()));
    }
}
