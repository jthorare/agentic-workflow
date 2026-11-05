using System.Text;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// MDL-SPEC-001 (model-check design §3, "Rules"): a spec folder holds only regular .tla, .cfg and .md files, plus a gaps/ folder of .cfg files, and every module it pulls in is one of TLC's standard modules
/// or another module of the same folder (so IOUtils, which can run a process, never loads). An honest-mistake guard, not a sandbox.
/// </summary>
internal static class SpecRules
{
    /// <summary>The rule id.</summary>
    internal const string Rule = "MDL-SPEC-001";

    private static readonly HashSet<string> _standard = new(["Naturals", "Integers", "Sequences", "FiniteSets", "Bags", "TLC"], StringComparer.Ordinal);

    /// <summary>What is wrong with <paramref name="folder"/>; empty when it may run.</summary>
    /// <param name="folder">The spec folder.</param>
    /// <returns>One finding per problem.</returns>
    internal static IReadOnlyList<RuleFinding> Check(SpecFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var findings = new List<RuleFinding>();
        var local = new HashSet<string>(folder.Files.Where(static f => f.Name.EndsWith(".tla", StringComparison.Ordinal) && !f.Name.Contains('/', StringComparison.Ordinal)).Select(static f => f.Name[..^4]), StringComparer.Ordinal);
        foreach (var file in folder.Files)
        {
            var path = $"{folder.Path}/{file.Name}";
            if (file.Kind != SpecFileKind.Regular)
            {
                findings.Add(new RuleFinding(Rule, $"{folder.Path} holds {file.Name}, which is {(file.Kind == SpecFileKind.Symlink ? "a symbolic link" : "not a regular file")}; a spec folder holds only regular files (fix: remove it)", path));
            }
            else if (!Allowed(file.Name))
            {
                findings.Add(new RuleFinding(Rule, $"{folder.Path} holds {file.Name}; a spec folder holds only .tla, .cfg and .md files, and gaps/ only .cfg files (fix: remove it)", path));
            }
            else if (file.Name.EndsWith(".tla", StringComparison.Ordinal))
            {
                findings.AddRange(Modules(StripComments(file.Text)).Where(m => !_standard.Contains(m) && !local.Contains(m))
                    .Select(m => new RuleFinding(Rule, $"{folder.Path}/{file.Name} pulls in the module {m}, which is neither one of TLC's standard modules nor in this folder (fix: remove it; a spec may not load other code)", path)));
            }
        }

        return findings;
    }

    private static bool Allowed(string name) => name.StartsWith("gaps/", StringComparison.Ordinal)
        ? name.EndsWith(".cfg", StringComparison.Ordinal) && !name["gaps/".Length..].Contains('/', StringComparison.Ordinal)
        : !name.Contains('/', StringComparison.Ordinal) && (name.EndsWith(".tla", StringComparison.Ordinal) || name.EndsWith(".cfg", StringComparison.Ordinal) || name.EndsWith(".md", StringComparison.Ordinal));

    // '\*' to the end of the line and '(* ... *)' (which nests in TLA+) become blanks, so a comment can neither hide a module nor name one.
    private static string StripComments(string text)
    {
        var result = new StringBuilder(text.Length);
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (At(text, i, "(*"))
            {
                depth++;
                i++;
            }
            else if (depth > 0 && At(text, i, "*)"))
            {
                depth--;
                i++;
            }
            else if (depth == 0 && At(text, i, "\\*"))
            {
                i = text.IndexOf('\n', i) is var end and >= 0 ? end - 1 : text.Length;   // the newline itself stays
            }
            else if (depth == 0 || text[i] == '\n')
            {
                _ = result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    private static bool At(string text, int index, string what) => string.CompareOrdinal(text, index, what, 0, what.Length) == 0;

    // The modules named by EXTENDS lists (across lines) and by INSTANCE / LOCAL INSTANCE.
    private static IEnumerable<string> Modules(string text)
    {
        var tokens = Tokens(text);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i], "EXTENDS", StringComparison.Ordinal))
            {
                var at = i + 1;
                while (at < tokens.Count && IsName(tokens[at]))
                {
                    yield return tokens[at];
                    at++;
                    if (at < tokens.Count && string.Equals(tokens[at], ",", StringComparison.Ordinal))
                    {
                        at++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else if (string.Equals(tokens[i], "INSTANCE", StringComparison.Ordinal) && i + 1 < tokens.Count && IsName(tokens[i + 1]))
            {
                yield return tokens[i + 1];
            }
        }
    }

    private static bool IsName(string token) => token.Length > 0 && (char.IsLetter(token[0]) || token[0] == '_');

    private static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                _ = word.Append(c);
                continue;
            }

            if (word.Length > 0)
            {
                tokens.Add(word.ToString());
                _ = word.Clear();
            }

            if (c == ',')
            {
                tokens.Add(",");
            }
        }

        if (word.Length > 0)
        {
            tokens.Add(word.ToString());
        }

        return tokens;
    }
}
