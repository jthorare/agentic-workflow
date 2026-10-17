using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 0600 settings file behind the 'claude-settings-file' policy form (launcher L-17(a), owner 2026-10-17): the launcher renders the target-neutral
/// <see cref="StepPolicy"/> and deletes the file with the launch. ReadOnly is plan mode plus the allow-list and the one writable path; Plan is plan mode
/// with no write rules; Default sets no mode and adds the allow-list and the writable path.
/// </summary>
internal static class StepPolicyFile
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(LauncherJsonContext.Default, indented: true);

    /// <summary>The file's text for <paramref name="policy"/>.</summary>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The settings JSON.</returns>
    internal static byte[] Render(StepPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var allow = new List<string>(policy.AllowedTools);
        string[]? directories = null;
        if (policy.Mode != StepPermissionMode.Plan && policy.WritablePath is { } path)
        {
            allow.AddRange([$"Edit(/{path}/**)", $"Write(/{path}/**)"]);
            directories = [path];
        }

        var permissions = new ClaudePermissions(allow) { DefaultMode = policy.Mode == StepPermissionMode.Default ? null : "plan", AdditionalDirectories = directories };
        return [.. AwJson.Serialize(new ClaudeSettingsDocument(permissions), _json), .. "\n"u8];
    }

    /// <summary>Where the launch's settings file lives under <paramref name="directory"/>.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The file's path.</returns>
    internal static string PathFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".settings.json");

    /// <summary>Writes the file 0600 (the directory 0700) through a temp file and a rename.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The file's path.</returns>
    internal static string Write(string directory, LaunchId launch, StepPolicy policy)
    {
        OwnerOnly.EnsureDirectory(directory);
        var path = PathFor(directory, launch);
        OwnerOnly.WriteAtomically(path, Render(policy));
        return path;
    }

    /// <summary>Deletes the launch's file; a missing one is fine.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch) => File.Delete(PathFor(directory, launch));
}
