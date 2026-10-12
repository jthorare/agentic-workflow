using System.Globalization;
using Aw.Redaction;
using Aw.Redaction.AotSmoke;

// Default: scrub the built-in samples and the extra-pattern samples and print one line each (run.sh compares the
// output with expected.txt).
// With "--stdin [--extra <id>=<pattern>]... [--timeout-ms <n>]": scrub standard input with the built-in rules and the
// extra patterns, write the text to stdout and the outcome and hits to stderr. Exit 3 when blocked, 2 when an extra
// pattern is refused (each refusal on stderr) or the arguments are wrong.
if (args is ["--stdin", ..])
{
    if (!StdinOptions.TryParse(args.AsSpan(1), out var options))
    {
        await Console.Error.WriteLineAsync("usage: --stdin [--extra <id>=<pattern>]... [--timeout-ms <n>]").ConfigureAwait(false);
        return 2;
    }

    var created = Redactor.Create(options.Extras, options.Timeout, TimeProvider.System);
    if (!created.IsValid)
    {
        await Console.Error.WriteLineAsync(string.Join('\n', created.Errors.Select(Refusal))).ConfigureAwait(false);
        return 2;
    }

    var result = created.Redactor.Scrub(await Console.In.ReadToEndAsync().ConfigureAwait(false));
    await Console.Out.WriteAsync(result.Text).ConfigureAwait(false);
    await Console.Error.WriteLineAsync(Describe(result)).ConfigureAwait(false);
    return result.IsBlocked ? 3 : 0;
}

foreach (var (name, input) in Samples.All)
{
    Print(name, Redactor.Default.Scrub(input));
}

foreach (var (name, extras, timeout, input) in Samples.Extra)
{
    var created = Redactor.Create(extras, timeout, TimeProvider.System);
    if (created.IsValid)
    {
        Print(name, created.Redactor.Scrub(input));
    }
    else
    {
        Console.Out.Write($"{name}: refused | {string.Join("; ", created.Errors.Select(Refusal))}\n");
    }
}

return 0;

static void Print(string name, ScrubResult result)
{
    var text = result.Text.Replace("\n", "\\n", StringComparison.Ordinal);
    Console.Out.Write($"{name}: {text} | {Describe(result)}\n");
}

static string Describe(ScrubResult result) =>
    string.Join(' ', result.Hits.Select(h => string.Create(CultureInfo.InvariantCulture, $"{h.Rule}@{h.Start}-{h.End}"))
        .Prepend(result.Outcome.ToString().ToLowerInvariant())
        .Concat(result.BlockCode is null ? [] : [result.BlockCode]));

static string Refusal(ExtraPatternError error) =>
    string.Create(CultureInfo.InvariantCulture, $"{error.Code} [{error.Index}].{error.Field.ToString().ToLowerInvariant()}: {error.Message}");
