using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The `dotnet`, `npm` and `npx` shims of the sandbox (sandbox spec §3.3 rule 2, docs/csharp/sindri-sandbox.md "The shims"): three POSIX sh scripts that
/// call `heavy --expensive|--light /abs/tool` explicitly, because heavy classifies by the literal command text and an explicit `--light` would otherwise
/// bypass the box-wide cap. The table is checked against Golden/Sandbox/shims.json and against the case list in `bin-r1b/heavy`.
/// </summary>
internal static class SandboxShims
{
    /// <summary>The shims, in the order they are written.</summary>
    internal static readonly IReadOnlyList<string> Tools = ["dotnet", "npm", "npx"];

    /// <summary>Text that makes heavy treat a command as expensive wherever it appears (heavy's `*AgenticWorkflow.slnx*|...` line); the shims route it to --expensive too.</summary>
    internal static readonly IReadOnlyList<string> AnywhereMarkers = ["AgenticWorkflow.slnx", "aot-smoke", "playwright", "ui-evidence", "Aw.Evidence", " e2e.sh ", "vitest"];

    private static readonly Dictionary<string, string[]> _expensive = new(StringComparer.Ordinal)
    {
        ["dotnet"] = ["build", "publish", "format", "pack", "restore", "run"],
        ["npm"] = ["ci", "install", "test"],
        ["npx"] = ["vitest", "tsc"],
    };

    private static readonly Dictionary<string, string[]> _direct = new(StringComparer.Ordinal)
    {
        ["dotnet"] = ["--version", "--info", "--list-sdks", "--list-runtimes"],
        ["npm"] = ["--version"],
        ["npx"] = [],
    };

    // Options whose next argument is their value, so it is never the subcommand.
    private static readonly Dictionary<string, string[]> _valueOptions = new(StringComparer.Ordinal)
    {
        ["dotnet"] = ["-v", "--verbosity"],
        ["npm"] = ["--prefix", "-C", "--workspace", "-w"],
        ["npx"] = ["-p", "--package"],
    };

    /// <summary>The subcommands a shim always sends to `heavy --expensive`.</summary>
    internal static IReadOnlyList<string> ExpensiveSubcommands(string tool) => _expensive[tool];

    /// <summary>The first-argument flags a shim runs directly, with no heavy.</summary>
    internal static IReadOnlyList<string> DirectFlags(string tool) => _direct[tool];

    /// <summary>Writes one executable script per shim whose real tool is in <paramref name="realTools"/> (a shim name to the absolute path of the real tool) into <paramref name="directory"/>; a tool missing from the map gets none.</summary>
    internal static void Write(string directory, IReadOnlyDictionary<string, string> realTools, string heavyPath)
    {
        Directory.CreateDirectory(directory);
        foreach (var tool in Tools)
        {
            if (!realTools.TryGetValue(tool, out var realPath))
            {
                continue;
            }

            var path = Path.Combine(directory, tool);
            WriteExecutable(path, Script(tool, realPath, heavyPath));
        }
    }

    private static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, ExecutableMode);
        }
    }

    /// <summary>The file name of the git credential helper shim (SB-7): the plan's own, so its `credential.helper` entry and the file can't drift apart.</summary>
    internal const string CredentialHelperName = GitSettings.CredentialHelperName;

    private const UnixFileMode ExecutableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// Writes the agent-app shims (SB-7): the git credential helper, and `gh` when <paramref name="realGh"/> is known. Both read <paramref name="tokenFile"/> at each call,
    /// so a refreshed token is seen without a restart and the token is never in the agent's environment or in an argument list.
    /// </summary>
    internal static void WriteGithub(string directory, string? realGh, string tokenFile)
    {
        Directory.CreateDirectory(directory);
        WriteExecutable(Path.Combine(directory, CredentialHelperName), CredentialHelperScript(tokenFile));
        if (realGh is not null)
        {
            WriteExecutable(Path.Combine(directory, "gh"), GhScript(realGh, tokenFile));
        }
    }

    /// <summary>The `gh` shim: runs the real gh with the token in gh's own environment only.</summary>
    internal static string GhScript(string realGh, string tokenFile) =>
        "#!/bin/sh\n# Sindri sandbox shim for gh: the agent GitHub App token goes into gh's own environment, never the agent's (docs/csharp/sindri-sandbox.md).\n"
        + "real=" + Quote(realGh) + "\ntoken_file=" + Quote(tokenFile) + "\n"
        + "token=$(cat \"$token_file\" 2>/dev/null) || token=\n"
        + "if [ -z \"$token\" ]; then echo \"gh: the GitHub token is not available in this Step ($token_file is missing or empty). " + SandboxStatusLine.Refusal + "\" >&2; exit 1; fi\n"
        + "GH_TOKEN=$token\nexport GH_TOKEN\nexec \"$real\" \"$@\"\n";

    /// <summary>The git credential helper: answers `get` for https://github.com only; `store` and `erase` read their input and do nothing.</summary>
    internal static string CredentialHelperScript(string tokenFile) =>
        "#!/bin/sh\n# Sindri sandbox git credential helper: the agent GitHub App token for github.com over https, read at each call.\n"
        + "token_file=" + Quote(tokenFile) + "\n"
        + "[ \"${1:-}\" = get ] || { cat >/dev/null; exit 0; }\n"
        + "proto=; host=\nwhile IFS= read -r line || [ -n \"$line\" ]; do\n  case \"$line\" in\n    protocol=*) proto=${line#protocol=} ;;\n    host=*) host=${line#host=} ;;\n  esac\ndone\n"
        + "[ \"$proto\" = https ] && [ \"$host\" = github.com ] || exit 0\n"
        + "token=$(cat \"$token_file\" 2>/dev/null) || token=\n"
        + "[ -n \"$token\" ] || exit 0\n"
        + "printf 'username=x-access-token\\npassword=%s\\n' \"$token\"\n";

    /// <summary>The agent-app pre-push hook: allows a push only to the run branch or under it, and never a delete.</summary>
    internal static string BranchOnlyPrePush(string runNamespace) =>
        "#!/bin/sh\n# Sindri sandbox pre-push: only the run branch can be pushed.\nns=" + Quote(runNamespace) + "\n"
        + "refuse() { echo \"git push to $1 is not allowed in this Step: only the run branch refs/heads/$ns can be pushed. " + SandboxStatusLine.Refusal + "\" >&2; exit 1; }\n"
        + "while read -r lref lsha rref rsha || [ -n \"$rref\" ]; do\n"
        + "  [ -n \"$ns\" ] || refuse \"$rref\"\n"
        + "  case \"$rref\" in \"refs/heads/$ns\"|\"refs/heads/$ns/\"*) ;; *) refuse \"$rref\" ;; esac\n"
        + "  case \"$lsha\" in *[!0]*) ;; *) refuse \"$rref\" ;; esac\n"
        + "done\nexit 0\n";

    /// <summary>The sh script of one shim.</summary>
    internal static string Script(string tool, string realPath, string heavyPath)
    {
        var script = new StringBuilder();
        script.Append("#!/bin/sh\n# Sindri sandbox shim for ").Append(tool).Append(": routes through heavy explicitly (docs/csharp/sindri-sandbox.md, \"The shims\").\n");
        script.Append("real=").Append(Quote(realPath)).Append("\nheavy=").Append(Quote(heavyPath)).Append('\n');
        script.Append("if [ \"${AW_SANDBOX_SLOT_HELD:-}\" = 1 ]; then exec \"$real\" \"$@\"; fi\n");
        var direct = DirectFlags(tool);
        if (direct.Count > 0)
        {
            script.Append("case \"${1:-}\" in ").AppendJoin('|', direct).Append(") exec \"$real\" \"$@\" ;; esac\n");
        }

        script.Append("case \" $* \" in\n  ")
            .AppendJoin('|', AnywhereMarkers.Select(m => "*" + Quote(m) + "*"))
            .Append(") exec \"$heavy\" --expensive \"$real\" \"$@\" ;;\nesac\n");
        script.Append("sub=; nobuild=0; skip=0\nfor a in \"$@\"; do\n  if [ \"$skip\" = 1 ]; then skip=0; continue; fi\n  case \"$a\" in\n    --no-build) nobuild=1 ;;\n    ")
            .AppendJoin('|', _valueOptions[tool])
            .Append(") skip=1 ;;\n    -*) ;;\n    *) [ -n \"$sub\" ] || sub=$a ;;\n  esac\ndone\n");
        script.Append("case \"$sub\" in\n  ").AppendJoin('|', ExpensiveSubcommands(tool)).Append(") exec \"$heavy\" --expensive \"$real\" \"$@\" ;;\n");
        if (string.Equals(tool, "dotnet", StringComparison.Ordinal))
        {
            script.Append("  test) [ \"$nobuild\" = 1 ] || exec \"$heavy\" --expensive \"$real\" \"$@\" ;;\n");
        }

        script.Append("esac\nexec \"$heavy\" --light \"$real\" \"$@\"\n");
        return script.ToString();
    }

    private static string Quote(string text) => $"'{text.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}
