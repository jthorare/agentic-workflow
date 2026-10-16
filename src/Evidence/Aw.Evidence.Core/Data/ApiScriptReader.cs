using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads an API script document and checks its rules.</summary>
public static partial class ApiScriptReader
{
    private static readonly SearchValues<char> _pathBreakers = SearchValues.Create("\\# \t\r\n?");

    /// <summary>Parses and validates a script.</summary>
    /// <param name="utf8Json">The script file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The script, or AW-EVIDENCE-017 naming the JSON location and every problem.</returns>
    public static Result<ApiScript> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<ApiScript>(utf8Json, options, EvidenceErrors.ApiScriptInvalid).Bind(Validate);

    /// <summary>Checks a script's rules: a known schema, at least one step, well-formed paths, headers, pointers and expectations.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The script, or AW-EVIDENCE-017 naming every problem.</returns>
    public static Result<ApiScript> Validate(ApiScript script)
    {
        var problems = new Problems();
        problems.AddIf(script.Schema is not (null or ApiScript.SchemaName), $"$.schema: must be {ApiScript.SchemaName} when given");
        problems.AddIf(script.RequestList.Count + script.CliList.Count == 0, "$: needs at least one request or CLI call");
        for (var i = 0; i < script.RequestList.Count; i++)
        {
            CheckRequest(script.RequestList[i], Item("$.requests", i), problems);
        }

        for (var i = 0; i < script.CliList.Count; i++)
        {
            CheckCall(script.CliList[i], Item("$.cli", i), problems);
        }

        CheckNames(script.PassEnv, "$.passEnv", problems);
        problems.AddIf(script.DeclaredWrites?.Any(string.IsNullOrWhiteSpace) == true, "$.declaredWrites: every word must be non-empty");
        return problems.Into(script, EvidenceErrors.ApiScriptInvalid);
    }

    private static void CheckRequest(RequestStep request, string at, Problems problems)
    {
        var path = request.Path;
        problems.AddIf(
            path.Length < 2 || path[0] != '/' || path[1] == '/' || path.AsSpan().IndexOfAny(_pathBreakers) >= 0,
            $"{at}: path must be a path on the host: one leading '/', not '//', no backslash, space, '?' or '#' (a query goes in the path only through the script's own request body or a later stage)");
        problems.AddIf(request.Body is not null && request.Method is HttpVerb.Get or HttpVerb.Head, $"{at}: body is not allowed on GET or HEAD");
        if (request.Headers is { } headers)
        {
            foreach (var (name, source) in headers)
            {
                problems.AddIf(!HeaderName.IsMatch(name), $"{at}.headers: '{name}' is not a header name");
                problems.AddIf(!EnvName.IsMatch(source.Env), $"{at}.headers.{name}: env must name an UPPER_SNAKE environment variable");
            }
        }

        problems.AddIf(request.Expect.Status is not (>= 100 and <= 599), $"{at}.expect: status is required, 100 to 599");
        problems.AddIf(request.Expect.ExitCode is not null, $"{at}.expect: exitCode belongs to CLI calls; a request takes status");
        CheckExpectation(request.Expect, $"{at}.expect", problems);
    }

    private static void CheckCall(CliStep call, string at, Problems problems)
    {
        problems.AddIf(call.Argv.Count == 0 || string.IsNullOrEmpty(call.Argv[0]), $"{at}: argv needs the program first");
        problems.AddIf(call.Expect.ExitCode is not (>= 0 and <= 255), $"{at}.expect: exitCode is required, 0 to 255");
        problems.AddIf(call.Expect.Status is not null, $"{at}.expect: status belongs to requests; a CLI call takes exitCode");
        CheckExpectation(call.Expect, $"{at}.expect", problems);
    }

    private static void CheckExpectation(Expectation expect, string at, Problems problems)
    {
        problems.AddIf(expect.Golden is { Length: 0 }, $"{at}: golden must name a file");
        var pointers = expect.Pointers ?? [];
        for (var i = 0; i < pointers.Count; i++)
        {
            CheckPointer(pointers[i], Item(at + ".pointers", i), problems);
        }
    }

    internal static void CheckPointer(PointerCheck check, string at, Problems problems)
    {
        if (JsonPointer.Problem(check.Location) is { } bad)
        {
            problems.Add($"{at}: pointer {bad}");
        }

        var given = (check.EqualTo is null ? 0 : 1) + (check.Exists is null ? 0 : 1) + (check.Matches is null ? 0 : 1);
        problems.AddIf(given != 1, $"{at}: give exactly one of equals, exists and matches");
        if (check.Matches is { } pattern && !IsRegex(pattern))
        {
            problems.Add($"{at}: matches is not a valid regular expression");
        }
    }

    private static void CheckNames(IReadOnlyList<string>? names, string at, Problems problems)
    {
        foreach (var name in names ?? [])
        {
            problems.AddIf(!EnvName.IsMatch(name), $"{at}: '{name}' must be an UPPER_SNAKE environment variable name");
        }
    }

    internal static string Item(string list, int index) => list + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    private static bool IsRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, Matching.Timeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HeaderName { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnvName { get; }
}
