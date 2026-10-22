using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Marks a Step's worktree trusted in Claude Code's own config (<c lang="text">projects[&lt;path&gt;].hasTrustDialogAccepted</c>), so the CLI doesn't
/// stop at "is this a project you trust?" with nobody to answer. The edit touches that one key: it takes an advisory lock beside the file,
/// re-reads right before writing (a running Claude rewrites the same file), writes a temp file in the same directory and renames it over
/// the original keeping its mode, and writes nothing when there is nothing to change. A file that doesn't parse, or has a different shape
/// at those keys, is refused untouched.
/// </summary>
/// <param name="configPath">The config file (<c lang="text">~/.claude.json</c>, or <c lang="text">$CLAUDE_CONFIG_DIR/.claude.json</c>).</param>
internal sealed class ClaudeCodeTrust(string configPath) : IAgentTrust
{
    private const string Projects = "projects";
    private const string Accepted = "hasTrustDialogAccepted";
    private const int Attempts = 5;

    private static readonly JsonSerializerOptions _write = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Runs after the file is read and before the re-read; a test uses it to play the running Claude that rewrites the file in between.</summary>
    internal Action? AfterRead { get; init; }

    /// <summary>How long a held lock is waited for.</summary>
    internal TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Trust(string directory, CancellationToken cancellationToken) => Edit(directory, add: true, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Untrust(string directory, CancellationToken cancellationToken) => Edit(directory, add: false, cancellationToken);

    /// <summary>The config file Claude Code reads for a user: <c lang="text">$CLAUDE_CONFIG_DIR/.claude.json</c> when the variable is set, else <c lang="text">~/.claude.json</c>.</summary>
    /// <param name="environment">Reads a variable.</param>
    /// <returns>The path.</returns>
    internal static string PathFor(Func<string, string?> environment) =>
        environment("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir
            ? Path.Combine(dir, ".claude.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    private static Error Refused(string why) =>
        SindriErrors.LaunchTrustUnavailable.With(why + " (fix: accept the trust prompt once by hand, or repair the file; Sindri never rewrites a file it can't parse)");

    private async ValueTask<Result<Unit>> Edit(string directory, bool add, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Path.IsPathRooted(directory))
        {
            return KernelErrors.Invalid.With("A trusted directory is an absolute path");
        }

        var key = Path.GetFullPath(directory).TrimEnd('/');
        try
        {
            if (add)
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? ".");
            }
            else if (!File.Exists(configPath))
            {
                return Results.Ok();
            }

            var gate = await Lock(cancellationToken).ConfigureAwait(false);
            if (gate is null)
            {
                return Refused($"{configPath}.sindri.lock stayed held for {LockWait.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s");
            }

            await using (gate.ConfigureAwait(false))
            {
                return Apply(key, add);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Refused($"{configPath} couldn't be updated: {e.Message}");
        }
    }

    private Result<Unit> Apply(string key, bool add)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var read = Read();
            var parsed = Parse(read);
            if (parsed.IsError)
            {
                return parsed.Error;
            }

            var root = parsed.Value;
            var changed = add ? Mark(root, key) : Remove(root, key);
            if (changed.IsError)
            {
                return changed.Error;
            }

            if (!changed.Value)
            {
                return Results.Ok();   // already so: nothing is written
            }

            AfterRead?.Invoke();
            if (!Read().AsSpan().SequenceEqual(read))
            {
                continue;   // the file moved under us: apply to what it is now
            }

            Write(root, read);
            return Results.Ok();
        }

        return Refused($"{configPath} kept changing while it was updated");
    }

    private static Result<JsonObject> Parse(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(bytes) is JsonObject root ? root : Refused("the file's top level isn't a JSON object");
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            return Refused("the file isn't valid JSON: " + e.Message);
        }
    }

    private static Result<bool> Mark(JsonObject root, string key)
    {
        var projects = Child(root, Projects, create: true);
        if (projects.IsError)
        {
            return projects.Error;
        }

        var entry = Child(projects.Value ?? [], key, create: true);   // create: true never answers null; the fallback only satisfies nullability
        if (entry.IsError)
        {
            return entry.Error;
        }

        var trusted = entry.Value ?? [];
        if (trusted[Accepted] is JsonValue current && current.TryGetValue<bool>(out var accepted) && accepted)
        {
            return false;
        }

        trusted[Accepted] = true;
        return true;
    }

    private static Result<bool> Remove(JsonObject root, string key)
    {
        var projects = Child(root, Projects, create: false);
        if (projects.IsError)
        {
            return projects.Error;
        }

        return projects.Value?.Remove(key) ?? false;
    }

    private static Result<JsonObject?> Child(JsonObject parent, string name, bool create)
    {
        switch (parent[name])
        {
            case JsonObject existing:
                return existing;
            case null when !parent.ContainsKey(name):
                if (!create)
                {
                    return (JsonObject?)null;
                }

                var made = new JsonObject();
                parent[name] = made;
                return made;
            default:
                return Refused($"'{name}' in the file isn't an object");
        }
    }

    private byte[] Read() => File.Exists(configPath) ? File.ReadAllBytes(configPath) : [];

    private void Write(JsonObject root, byte[] original)
    {
        var text = root.ToJsonString(_write);
        if (original.Length > 0 && original[^1] == (byte)'\n')
        {
            text += "\n";
        }

        var temp = string.Create(CultureInfo.InvariantCulture, $"{configPath}.sindri-tmp-{Environment.ProcessId}-{Guid.NewGuid():N}");
        var mode = ModeOf(configPath);
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temp, options))
            {
                stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temp, mode);
            }

            File.Move(temp, configPath, overwrite: true);
        }
        catch
        {
            _ = OwnerOnly.TryDelete(temp);
            throw;
        }
    }

    /// <summary>The file's mode, kept across the rewrite; a new file is owner-only (it holds the user's login state).</summary>
    private static UnixFileMode ModeOf(string path) =>
        !OperatingSystem.IsWindows() && File.Exists(path) ? File.GetUnixFileMode(path) : UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private async ValueTask<FileStream?> Lock(CancellationToken cancellationToken)
    {
        var path = configPath + ".sindri.lock";
        var step = TimeSpan.FromMilliseconds(25);
        for (var waited = TimeSpan.Zero; ; waited += step)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (waited < LockWait)
            {
                await Task.Delay(step, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
