using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>Expands an argv template over the closed placeholder set (launcher spec section 4). There is no shell: the result is an argument list.</summary>
public static partial class Placeholders
{
    private const string CmdElement = "{cmd...}";

    /// <summary>Expands each template element: '{cmd...}' alone splices its argv, any other placeholder expands to exactly one argument, even inside one.</summary>
    /// <param name="template">The argv template.</param>
    /// <param name="values">The placeholder values.</param>
    /// <returns>The arguments; SND-REQ-001 for an expanded title, name, session id or seed that would start an argument with '-' or a '{cwd}' that isn't absolute; SND-LAUNCH-011 for a placeholder with no value.</returns>
    public static Result<IReadOnlyList<string>> Expand(IReadOnlyList<string> template, PlaceholderValues values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        List<string> argv = [];
        foreach (var element in template)
        {
            if (string.Equals(element, CmdElement, StringComparison.Ordinal))
            {
                if (values.Cmd is null)
                {
                    return SindriErrors.LauncherInvalid.With("{cmd...} has no attach argv here (fix: use it only in a viewer template)");
                }

                argv.AddRange(values.Cmd);
                continue;
            }

            var expanded = ExpandElement(element, values);
            if (expanded.IsError)
            {
                return expanded.Error;
            }

            argv.Add(expanded.Value);
        }

        return argv;
    }

    private static Result<string> ExpandElement(string element, PlaceholderValues values)
    {
        Error? problem = null;
        var text = Token.Replace(element, m =>
        {
            var value = ValueOf(m.Groups["name"].Value, values, m.Index == 0, out var error);
            problem ??= error;
            return value ?? string.Empty;
        });
        return problem is null ? text : problem;
    }

    private static string? ValueOf(string name, PlaceholderValues values, bool first, out Error? error)
    {
        error = null;
        var (value, guarded) = name switch
        {
            "bin" => (values.Bin, false),
            "cwd" => (values.Cwd, false),
            "title" => (values.Title, true),
            "name" => (values.Name, true),
            "seed" => (values.Seed, true),
            "sessionId" => (values.SessionId?.Value, true),
            "mcpConfig" => (values.McpConfig, false),
            _ => (null, false),
        };
        if (value is null)
        {
            error = SindriErrors.LauncherInvalid.With($"{{{name}}} has no value here (fix: the template uses a placeholder this launch has no value for)");
        }
        else if (guarded && first && value.StartsWith('-'))
        {
            error = SindriErrors.RequestInvalid.With($"the {name} would start an argument with '-' and be taken for an option (fix: change the {name})");
        }
        else if (string.Equals(name, "cwd", StringComparison.Ordinal) && !IsAbsolute(value))
        {
            error = SindriErrors.RequestInvalid.With("the working directory isn't absolute (fix: pass the repo's absolute path)");
        }

        return value;
    }

    /// <summary>Whether a path is absolute: rooted with '/' or '\\', or a drive letter and a separator. Core does no I/O, so this is text only.</summary>
    /// <param name="path">The path.</param>
    /// <returns>True when it is absolute.</returns>
    public static bool IsAbsolute(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Length > 0 && (path[0] is '/' or '\\' || (path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\'));
    }

    [GeneratedRegex("\\{(?<name>bin|cwd|title|name|seed|sessionId|mcpConfig|cmd\\.\\.\\.)\\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
