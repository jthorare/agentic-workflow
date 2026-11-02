using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// The filters a model-pick input passes before any provider sees it (docs/csharp/judge.md): a file or fact under a denyPaths
/// glob is dropped as denied; one with a built-in secret file name, or whose text the secret scan flags, is dropped whole as
/// secret; a secret in the brief is masked at its span and the rest is sent. What is left is cut to the size cap, whole files
/// at a time. Dropped files are counted in the input that is sent and named only in <see cref="ModelPickInput.Dropped"/>, which
/// is never serialized.
/// </summary>
public static class ModelPickPack
{
    /// <summary>Why a file was dropped by name or path.</summary>
    public const string Denied = "denied";

    /// <summary>Why a file was dropped for a secret name or a secret in its text.</summary>
    public const string Secret = "secret";

    // Names only, matched at any depth: the built-in list of the owner's decision (q123).
    private static readonly string[] _secretNames = [".env*", "*.pem", "*.key", "*.p12", "id_rsa*", "secrets*", "credentials*", "*.tfvars"];

    /// <summary>Filters and caps an input.</summary>
    /// <param name="input">The call's validated input.</param>
    /// <param name="redact">The judge's redactor (IInputRedactor.Redact), which is also the secret scan: text it changes holds a secret.</param>
    /// <returns>The input to send: kept files, the brief masked, and the omitted, cut and withheld records set (any the caller sent are replaced).</returns>
    public static ModelPickInput Apply(ModelPickInput input, Func<JsonNode?, JsonNode?> redact)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(redact);
        List<ModelPickFile> kept = [];
        List<ModelPickOmitted> dropped = [];
        foreach (var file in input.Files)
        {
            if (Why(file, input.DenyPaths, redact) is { } why)
            {
                dropped.Add(new ModelPickOmitted(file.Path, file.Text.Length, why));
            }
            else
            {
                kept.Add(file);
            }
        }

        var brief = UntrustedText.Capped(Masked(input.Brief, redact), ModelPickQuestion.PackCap);
        var capped = UntrustedText.CappedFiles(kept, ModelPickQuestion.PackCap - brief.Length);
        return input with
        {
            Brief = brief,
            Files = capped.Kept,
            DenyPaths = null,
            Omitted = capped.Omitted,
            Cut = capped.Cut,
            Withheld = new ModelPickWithheld(dropped.Count(static d => string.Equals(d.Why, Secret, StringComparison.Ordinal)), dropped.Count(static d => string.Equals(d.Why, Denied, StringComparison.Ordinal))),
            Dropped = dropped,
        };
    }

    private static string? Why(ModelPickFile file, IReadOnlyList<string>? denyPaths, Func<JsonNode?, JsonNode?> redact)
    {
        if (denyPaths?.Any(glob => ModelPickGlob.IsMatch(glob, file.Path)) == true)
        {
            return Denied;
        }

        return _secretNames.Any(name => ModelPickGlob.IsMatch(name, file.Path)) || !string.Equals(Masked(file.Text, redact), file.Text, StringComparison.Ordinal) ? Secret : null;
    }

    // The text with its secrets replaced: equal to the text when it has none.
    private static string Masked(string text, Func<JsonNode?, JsonNode?> redact) =>
        redact(JsonValue.Create(text)) is JsonValue masked && masked.TryGetValue<string>(out var value) ? value : text;
}
