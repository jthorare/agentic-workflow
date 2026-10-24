using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Redaction;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Runs one Step input in its own scope and shapes what it returned (plan decisions 2 and 7): section names checked, the
/// text scrubbed before it is hashed (so the digest covers exactly what is served). No size cap: the text stays in this process
/// (only its bytes and digest travel through Temporal), so a big map's view is a long section, not a failure. The activity and the pull route's refill share this one path.
/// </summary>
/// <param name="dispatch">Runs the input in its own scope (the registration builds it).</param>
/// <param name="runtimes">The inputs this build runs.</param>
/// <param name="redactor">Scrubs the text; the default when none is given.</param>
internal sealed partial class StepInputRunner(InputDispatch dispatch, StepInputRuntimes runtimes, IRedactor? redactor = null) : IStepInputRunner
{
    /// <inheritdoc/>
    public async ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!runtimes.Has(context.Input))
        {
            return WorkflowErrors.StepInputNoRuntime.With($"input {context.Input.Value} has no runtime in this build");
        }

        var sections = await dispatch(context, cancellationToken).ConfigureAwait(false);
        return sections.IsError ? sections.Error : Shape(context, index, sections.Value);
    }

    private Result<FilledOutput> Shape(StepInputContext context, int index, InjectionSections filled)
    {
        var scrubber = redactor ?? Redactor.Default;
        SortedDictionary<string, string> texts = new(StringComparer.Ordinal);
        List<InjectionEntry> entries = [];
        foreach (var (name, text) in filled.Sections.OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            if (!SectionName.IsMatch(name))
            {
                return WorkflowErrors.InjectionInvalid.With($"input {context.Input.Value}: section name '{name}' is not [a-z][a-z0-9._-]{{0,63}}");
            }

            var clean = scrubber.Scrub(text).Text;
            var bytes = Encoding.UTF8.GetByteCount(clean);
            texts[name] = clean;
            entries.Add(new InjectionEntry(name, context.Input, bytes, Sha(clean)));
        }

        var shaped = Typed(context, filled.Result, scrubber);
        if (shaped.IsError)
        {
            return shaped.Error;
        }

        return new FilledOutput(new FilledInput(context.Input, index, entries, shaped.Value.Entry), new FilledTexts(texts, shaped.Value.Element));
    }

    private static Result<(InjectionResultEntry? Entry, JsonElement? Element)> Typed(StepInputContext context, JsonElement? result, IRedactor scrubber)
    {
        if (result is not { } element)
        {
            return (null, null);
        }

        var clean = scrubber.Scrub(element.GetRawText()).Text;
        var bytes = Encoding.UTF8.GetByteCount(clean);
        try
        {
            using var document = JsonDocument.Parse(clean);
            return (new InjectionResultEntry(bytes, Sha(clean)), document.RootElement.Clone());
        }
        catch (JsonException) // boundary: a placeholder that swallowed a quote; the result can't be kept
        {
            return WorkflowErrors.InjectionInvalid.With($"input {context.Input.Value}: the typed result is not JSON once scrubbed");
        }
    }

    internal static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [GeneratedRegex("^[a-z][a-z0-9._-]{0,63}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SectionName { get; }
}
