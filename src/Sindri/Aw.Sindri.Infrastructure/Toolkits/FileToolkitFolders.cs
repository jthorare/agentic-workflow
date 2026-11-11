using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>
/// The toolkits root on disk ($AW_SINDRI_HOME/toolkits: one folder per toolkit, named after it), and the generator run that fills it: 'node scripts/scaffold/new-toolkit.mjs' from the installed toolkit
/// ($AW_TOOLKIT, else ~/.agentic-workflow/toolkit), into the new folder, with the SDK feed Sindri packs ($AW_SINDRI_SDK_FEED, else $AW_SINDRI_HOME/sdk/packages).
/// </summary>
/// <param name="sindriHome">Sindri's home folder.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="runner">Runs node.</param>
internal sealed class FileToolkitFolders(string sindriHome, Func<string, string?> environment, IToolRunner runner) : IToolkitFolders
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(30);   // restore, a build and one test run, queued behind other heavy jobs

    private const int OutputCap = 800;

    private readonly string _root = Path.Combine(sindriHome, "toolkits");

    public IReadOnlyList<ToolkitFolder> Folders()
    {
        try
        {
            return Directory.Exists(_root)
                ? [.. Directory.EnumerateDirectories(_root)
                    .Where(static d => Directory.EnumerateFiles(d, "*.Toolkit.csproj", SearchOption.TopDirectoryOnly).Any())
                    .Select(static d => new ToolkitFolder(Path.GetFileName(d), d))
                    .OrderBy(static f => f.Name, StringComparer.Ordinal)]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];   // an unreadable root adopts nothing; the next serve start looks again
        }
    }

    public async ValueTask<Result<ToolkitFolder>> Generate(string name, string tool, string? needs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var toolkit = environment("AW_TOOLKIT") is { Length: > 0 } set ? set : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow", "toolkit");
        var script = Path.Combine(toolkit, "scripts", "scaffold", "new-toolkit.mjs");
        if (!File.Exists(script))
        {
            return SindriErrors.ToolkitCreateFailed.With($"the generator isn't at {script} (fix: run setup.sh, or set AW_TOOLKIT to the toolkit checkout)");
        }

        if (ToolNames.GeneratorName(name) is not { } pascal)
        {
            return SindriErrors.ToolkitNameInvalid.With($"'{name}' isn't a name the generator keeps (fix: pick another name)");
        }

        var dir = Path.Combine(_root, name);
        var feed = environment("AW_SINDRI_SDK_FEED") is { Length: > 0 } given ? given : Path.Combine(sindriHome, "sdk", "packages");
        List<string> arguments = [script, "--name", pascal, "--tool", tool, "--dir", dir, "--feed", feed];
        if (!string.IsNullOrEmpty(needs))
        {
            arguments.AddRange(["--needs", needs]);
        }

        var ran = await Task.Run(() => runner.Run("node", arguments, _timeout, new ToolRunOptions(WorkingDirectory: toolkit), cancellationToken), cancellationToken).ConfigureAwait(false);
        if (ran.Outcome is ToolOutcome.TimedOut or ToolOutcome.Cancelled)
        {
            Remove(dir);   // a killed generator skips its own revert; a half-made folder would be adopted at the next serve start and block a retried create
        }

        return ran.Outcome switch
        {
            ToolOutcome.Exited when ran.ExitCode == 0 && HoldsProject(dir) => new ToolkitFolder(name, dir),
            ToolOutcome.Exited when ran.ExitCode == 0 => SindriErrors.ToolkitCreateFailed.With($"the generator exited 0 but {dir} holds no *.Toolkit.csproj (fix: run the generator by hand with the same arguments and read what it prints)"),   // #2692: a run that did nothing must not write a row
            ToolOutcome.Missing => SindriErrors.ToolkitCreateFailed.With("node isn't installed (fix: install Node, see .nvmrc)"),
            ToolOutcome.TimedOut => SindriErrors.ToolkitCreateFailed.With($"the generator ran past {_timeout.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} minutes"),
            ToolOutcome.Cancelled or ToolOutcome.Exited => SindriErrors.ToolkitCreateFailed.With(Tail(string.IsNullOrWhiteSpace(ran.Error) ? ran.Output : ran.Error)),
            _ => SindriErrors.ToolkitCreateFailed.With("the generator didn't finish"),
        };
    }

    private static bool HoldsProject(string dir)
    {
        try
        {
            return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.Toolkit.csproj", SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Remove(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // best effort: the error the caller gets already says the generator didn't finish
        }
    }

    private static string Tail(string text) => text.Length <= OutputCap ? text.Trim() : "..." + text[^OutputCap..].Trim();
}
