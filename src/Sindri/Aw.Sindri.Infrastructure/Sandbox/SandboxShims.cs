using System.Text;

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
            File.WriteAllText(path, Script(tool, realPath, heavyPath));
            if (OperatingSystem.IsWindows())
            {
                continue;
            }

            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

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
