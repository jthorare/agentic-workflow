using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>The real machine (a write creates its temporary file with the target's mode, else 0600, before any text goes in): PATH lookups, the providers' config files under the settings' home, and git for the tracked check.</summary>
/// <param name="settings">Where to look.</param>
internal sealed class ProviderHost(McpSettings settings) : IProviderHost
{
    public bool Installed(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => OnPath(settings.ClaudeBin),
        ProviderFile.Codex => OnPath("codex"),
        ProviderFile.Cursor => OnPath("cursor-agent") || Directory.Exists(Path.Combine(settings.Home, ".cursor")),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
    };

    public string UserConfigPath(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => Path.Combine(settings.ClaudeConfigDir ?? settings.Home, ".claude.json"),
        ProviderFile.Codex => Path.Combine(settings.CodexHome ?? Path.Combine(settings.Home, ".codex"), "config.toml"),
        ProviderFile.Cursor => Path.Combine(settings.Home, ".cursor", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
    };

    public async ValueTask<string?> Read(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;

    public async ValueTask Write(string path, string text, CancellationToken cancellationToken)
    {
        var target = Target(path);
        var directory = Path.GetDirectoryName(target) ?? target;
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".tmp");
        try
        {
            File.Delete(temporary);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = FileModes.TryRead(target) ?? (UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream);
                await using (writer.ConfigureAwait(false))
                {
                    await writer.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }

            if (!OperatingSystem.IsWindows() && FileModes.TryRead(target) is { } targetMode)
            {
                File.SetUnixFileMode(temporary, targetMode);   // the umask may have narrowed the create mode
            }

            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public async ValueTask<bool> Tracked(string directory, string relativePath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        var start = ChildEnvironment.Start("git", ["-C", directory, "ls-files", "--error-unmatch", "--", relativePath]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        try
        {
            using var git = Process.Start(start);
            if (git is null)
            {
                return false;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var output = git.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = git.StandardError.ReadToEndAsync(timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
            return git.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;   // no git: nothing is tracked as far as we can tell
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return true;    // git didn't answer in time: unknown, so the file is treated as tracked and left alone
        }
    }

    /// <summary>The file a write lands in: a symlink's final target (so a linked dotfile keeps its link), else the full path.</summary>
    private static string Target(string path)
    {
        var full = Path.GetFullPath(path);
        return File.Exists(full) && new FileInfo(full).LinkTarget is not null
            ? File.ResolveLinkTarget(full, returnFinalTarget: true)?.FullName ?? full
            : full;
    }

    private bool OnPath(string name) =>
        name.Contains('/', StringComparison.Ordinal)
            ? File.Exists(name)
            : settings.SearchPath.Any(directory => File.Exists(Path.Combine(directory, name)));
}
