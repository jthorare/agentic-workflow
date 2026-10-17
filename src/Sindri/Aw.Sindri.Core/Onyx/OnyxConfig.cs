using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// What `sindri onyx up` writes beside the pinned compose file: the .env (the shipped template with six keys set) and a
/// compose override that binds nginx to loopback, runs every image by its pinned digest, sizes Onyx for one user and keeps
/// the Slack and Discord bots off. Pure text in, text out. The override is built as text, not through a YAML serializer,
/// because `!override` is a tag.
/// Bots off (a decision of the plan): the `background` service gets its own `command` that patches the image's own supervisord
/// config in a copy (`autostart=false` under [program:slack_bot] and [program:discord_bot]) and runs supervisord on the copy.
/// It needs no extra file to fetch or mount, which the manual page's copy-out-and-mount does (it needs the image pulled before
/// the config is written). The image's entrypoint script only runs `/usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf`
/// (backend/scripts/supervisord_entrypoint.sh at the pinned commit), so the replaced command starts the same process.
/// Sizing is spec §4.1 step 3 as in docs/sindri/onyx.md "A one-user dev box".
/// </summary>
public static class OnyxConfig
{
    private const string Header =
        "# Written by `sindri onyx up` and replaced on every run: change nothing here.\n"
        + "# Binds nginx to 127.0.0.1, runs every image by its pinned digest, sizes Onyx for one user and keeps the Slack and Discord bots off.\n"
        + "services:\n";

    private const string BotsOffCommand = """
            command: ["/bin/sh","-c","sed -e '/^\\[program:slack_bot\\]/a autostart=false' -e '/^\\[program:discord_bot\\]/a autostart=false' /etc/supervisor/conf.d/supervisord.conf > /tmp/supervisord.conf && exec /usr/bin/supervisord -c /tmp/supervisord.conf"]
        """;

    private const string SecretKey = "USER_AUTH_SECRET";

    /// <summary>Gets the six keys Sindri sets in the .env.</summary>
    public static IReadOnlyList<string> EnvKeys { get; } = ["IMAGE_TAG", "DISABLE_TELEMETRY", "AUTH_TYPE", SecretKey, "INDEXING_MODEL_SERVER_HOST", "HOST_PORT"];

    // Services the override gives a profile (replacing the compose file's), so compose doesn't start them unless that profile is enabled.
    private static readonly Dictionary<string, string> _profiledByOverride = new(StringComparer.Ordinal) { ["code-interpreter"] = "code-interpreter", ["indexing_model_server"] = "indexing-model-server" };

    // Services whose settings the override can't leave out: a compose without one has a layout this override wasn't written for.
    private static readonly string[] _sized = ["background", "indexing_model_server", "nginx", "opensearch"];

    /// <summary>Sets the six keys Sindri owns in the shipped env.template and keeps everything else as it is.</summary>
    /// <param name="template">The env.template (or a previous .env: rendering its own output changes nothing).</param>
    /// <param name="settings">The values.</param>
    /// <returns>The text with LF line endings and a trailing newline.</returns>
    public static string RenderEnv(string template, EnvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(settings);
        var lines = template.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines is [.., ""])
        {
            lines.RemoveAt(lines.Count - 1);
        }

        foreach (var (key, value) in Values(settings))
        {
            Set(lines, key, value);
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Reads USER_AUTH_SECRET from an existing .env, so a rerun keeps it.</summary>
    /// <param name="envText">The .env's text, or null when there is none.</param>
    /// <returns>The secret with its quotes removed, or null when the line is missing or empty.</returns>
    public static string? ReadSecret(string? envText)
    {
        var line = envText?.Split('\n').FirstOrDefault(static l => l.StartsWith(SecretKey + "=", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var value = line[(SecretKey.Length + 1)..].Trim();
        if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
        {
            value = value[1..^1];
        }

        return value.Length == 0 ? null : value;
    }

    /// <summary>Renders the compose override.</summary>
    /// <param name="services">The compose file's services.</param>
    /// <param name="settings">The values (the port).</param>
    /// <param name="pins">The lock's pins.</param>
    /// <param name="platform">The Docker server's platform, linux/arm64 or linux/amd64.</param>
    /// <returns>The override's text (services in ordinal order, so equal input gives equal bytes), or SND-ONYX-004 when an image isn't pinned or the compose lacks a service the sizing needs.</returns>
    public static Result<string> RenderOverride(IReadOnlyList<ComposeService> services, EnvSettings settings, OnyxPins pins, string platform)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        var images = PinnedImages(services, settings, pins, platform);
        if (images.IsError)
        {
            return images.Error;
        }

        if (_sized.FirstOrDefault(name => !images.Value.ContainsKey(name)) is { } missing)
        {
            return SindriErrors.OnyxPinsMalformed.With($"the compose layout changed: the pinned compose file has no service named {missing}, which Sindri's override sizes; update Sindri and context/onyx.lock together");
        }

        var text = new StringBuilder(Header);
        foreach (var (name, image) in images.Value)
        {
            _ = text.Append("  ").Append(name).Append(":\n    image: ").Append(image).Append('\n');
            foreach (var line in Settings(name, settings))
            {
                _ = text.Append(line).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Reads the enabled compose profiles out of a rendered .env: the comma-separated COMPOSE_PROFILES.</summary>
    /// <param name="envText">The .env's text, or null.</param>
    /// <returns>The profile names (empty when the line is missing, commented or empty); `*` enables every profile.</returns>
    public static IReadOnlyList<string> EnabledProfiles(string? envText)
    {
        var line = envText?.Split('\n').FirstOrDefault(static l => l.StartsWith("COMPOSE_PROFILES=", StringComparison.Ordinal));
        var value = line?["COMPOSE_PROFILES=".Length..].Trim() ?? "";
        if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
        {
            value = value[1..^1];
        }

        return [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>Lists the images compose runs: the services with no profile and the services with an enabled one, as the override leaves them. What `pull` must have fetched before Sindri starts the stack.</summary>
    /// <param name="services">The compose file's services.</param>
    /// <param name="settings">The values.</param>
    /// <param name="pins">The lock's pins.</param>
    /// <param name="platform">The Docker server's platform.</param>
    /// <param name="enabledProfiles">The profiles the rendered .env enables (see <see cref="EnabledProfiles"/>).</param>
    /// <returns>Each distinct `repository@digest` of an active service, ordinal order; SND-ONYX-004 when an image isn't pinned.</returns>
    public static Result<IReadOnlyList<string>> ActiveImages(IReadOnlyList<ComposeService> services, EnvSettings settings, OnyxPins pins, string platform, IReadOnlyList<string> enabledProfiles)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(enabledProfiles);
        var images = PinnedImages(services, settings, pins, platform);
        if (images.IsError)
        {
            return images.Error;
        }

        var all = enabledProfiles.Contains("*", StringComparer.Ordinal);
        bool Active(ComposeService service)
        {
            if (_profiledByOverride.TryGetValue(service.Name, out var own))
            {
                return all || enabledProfiles.Contains(own, StringComparer.Ordinal);
            }

            return service.Profiles.Count == 0 || all || service.Profiles.Any(p => enabledProfiles.Contains(p, StringComparer.Ordinal));
        }

        var active = services.Where(Active).Select(static s => s.Name).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> list = [.. images.Value.Where(i => active.Contains(i.Key)).Select(static i => i.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return Results.Success(list);
    }

    private static IEnumerable<(string Key, string Value)> Values(EnvSettings settings)
    {
        yield return ("IMAGE_TAG", settings.ImageTag);
        yield return ("DISABLE_TELEMETRY", "true");
        yield return ("AUTH_TYPE", "basic");
        yield return (SecretKey, settings.AuthSecret);
        yield return ("INDEXING_MODEL_SERVER_HOST", "inference_model_server");
        yield return ("HOST_PORT", settings.Port.ToString(CultureInfo.InvariantCulture));
    }

    // An uncommented KEY=… line is replaced (a duplicate is dropped); else a commented "# KEY=…" line is replaced in place; else the line is appended.
    private static void Set(List<string> lines, string key, string value)
    {
        var assignment = key + "=" + value;
        var active = lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.Ordinal));
        if (active >= 0)
        {
            lines[active] = assignment;
            for (var i = lines.Count - 1; i > active; i--)
            {
                if (lines[i].StartsWith(key + "=", StringComparison.Ordinal))
                {
                    lines.RemoveAt(i);
                }
            }

            return;
        }

        var commented = lines.FindIndex(l => l.TrimStart().StartsWith('#') && l.TrimStart().TrimStart('#').TrimStart().StartsWith(key + "=", StringComparison.Ordinal));
        if (commented >= 0)
        {
            lines[commented] = assignment;
        }
        else
        {
            lines.Add(assignment);
        }
    }

    // Every service (ordinal order) to `<repository>@<platform digest>`.
    private static Result<SortedDictionary<string, string>> PinnedImages(IReadOnlyList<ComposeService> services, EnvSettings settings, OnyxPins pins, string platform)
    {
        ArgumentNullException.ThrowIfNull(pins);
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["IMAGE_TAG"] = settings.ImageTag };
        var images = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var service in services.OrderBy(static s => s.Name, StringComparer.Ordinal))
        {
            if (service.Image is null)
            {
                return SindriErrors.OnyxPinsMalformed.With($"the compose service {service.Name} has no image, so Sindri can't pin it; the compose layout changed, so update Sindri and context/onyx.lock together");
            }

            var image = ComposeImageRef.Resolve(service.Image, env);
            if (image.IsError)
            {
                return image.Error;
            }

            var pin = pins.Images.FirstOrDefault(p => p.Repository == image.Value.Repository && p.Tag == image.Value.Tag && p.Platform == platform);
            if (pin is null)
            {
                return SindriErrors.OnyxPinsMalformed.With($"the compose service {service.Name} runs {image.Value.Repository}:{image.Value.Tag}, which context/onyx.lock doesn't pin for {platform}; Sindri only runs pinned images, so update the lock and Sindri together");
            }

            images[service.Name] = pin.Repository + "@" + pin.Digest;
        }

        return images;
    }

    // The lines the override adds under a service after its image.
    private static IEnumerable<string> Settings(string name, EnvSettings settings) => name switch
    {
        "background" =>
        [
            BotsOffCommand,
            "    depends_on:",
            "      indexing_model_server:",
            "        condition: service_started",
            "        required: false",
            "    environment:",
            "      - CELERY_WORKER_DOCFETCHING_CONCURRENCY=1",
            "      - CELERY_WORKER_DOCPROCESSING_CONCURRENCY=1",
            "      - CELERY_WORKER_LIGHT_CONCURRENCY=1",
        ],
        "code-interpreter" => ["    profiles: [\"code-interpreter\"]"],
        "indexing_model_server" => ["    profiles: [\"indexing-model-server\"]"],
        "nginx" => ["    ports: !override", string.Create(CultureInfo.InvariantCulture, $"      - \"127.0.0.1:{settings.Port}:80\"")],
        "opensearch" => ["    environment:", "      - \"OPENSEARCH_JAVA_OPTS=-Xms1g -Xmx1g\"", "    mem_limit: 2g"],
        _ => [],
    };
}
