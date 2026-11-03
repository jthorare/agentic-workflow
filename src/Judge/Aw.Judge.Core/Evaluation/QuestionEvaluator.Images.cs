using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The input's own 'images' and 'imagesBestEffort' (any question): read once, checked, and written to the row in their read form.</summary>
public sealed partial class QuestionEvaluator
{
    private const string AskedAsText = "; asked as text";

    // Reads the input's images once. A strict call (the default) with one bad image is refused whole ('invalid-images', one stderr line naming the
    // cause, no provider runs, the row keeps the paths as given); a best-effort call drops each bad image with a trace and a stderr line and goes on,
    // as text when none are left. A context with no reader or workspaces ignores the field, as a judge without this feature does.
    private async ValueTask<(IReadOnlyList<ImageAttachment> Images, EvaluationOutcome? Refused)> ReadGeneric<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, EvaluationContext context, CancellationToken cancellationToken)
        where TInput : class
    {
        if (run.Generic is not { } spec || context.ImageReader is not { } reader || context.ImageWorkspaces is null)
        {
            return ([], null);
        }

        var (kept, refusal) = await Collect(run, spec, reader, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return ([], await Refuse(question, run, spec, refusal, cancellationToken).ConfigureAwait(false));
        }

        run.GenericRead = kept;
        if (run.Input is JsonObject input)
        {
            _ = input.Remove("images");
            if (kept.Count > 0)
            {
                input["images"] = new JsonArray([.. kept.Select(image => (JsonNode?)JsonValue.Create(image.Path))]);
            }
        }

        return (kept, null);
    }

    // The images that stay, or the cause that refuses the call (strict); a best-effort call records each drop on the run.
    private static async ValueTask<(IReadOnlyList<ImageAttachment> Kept, string? Refusal)> Collect(
        EvaluationRun run, GenericImages spec, IImageReader reader, CancellationToken cancellationToken)
    {
        if (spec.Problem is not null)
        {
            return Unreadable(run, spec);
        }

        var (paths, tooMany) = Capped(run, spec);
        if (tooMany is not null)
        {
            return ([], tooMany);
        }

        var kept = new List<ImageAttachment>(paths.Count);
        long total = 0;
        for (var i = 0; i < paths.Count; i++)
        {
            var (image, cause) = await Admit(reader, spec, paths[i], (i, total), cancellationToken).ConfigureAwait(false);
            if (image is not null)
            {
                total += image.Bytes.Length;
                kept.Add(image);
            }
            else if (spec.BestEffort)
            {
                run.DropCauses.Add(cause);
            }
            else
            {
                return ([], cause);
            }
        }

        return total > GenericImages.MaxBytes
            ? ([], string.Create(CultureInfo.InvariantCulture, $"images total {Math.Round(total / 1048576d, MidpointRounding.AwayFromZero)} MiB; at most 24 MiB"))
            : (kept, null);
    }

    // One image read and checked: the image, or the cause it is not taken (a best-effort call also drops one that would take the total over 24 MiB).
    private static async ValueTask<(ImageAttachment? Image, string Cause)> Admit(
        IImageReader reader, GenericImages spec, string path, (int Index, long Total) at, CancellationToken cancellationToken)
    {
        var (image, cause) = await ReadOne(reader, path, at.Index, cancellationToken).ConfigureAwait(false);
        return image is not null && spec.BestEffort && at.Total + image.Bytes.Length > GenericImages.MaxBytes
            ? (null, string.Create(CultureInfo.InvariantCulture, $"images[{at.Index}] {path} would take the images over 24 MiB"))
            : (image, cause);
    }

    // A field that is not a list of paths refuses a strict call; a best-effort one drops it and asks as text.
    private static (IReadOnlyList<ImageAttachment> Kept, string? Refusal) Unreadable(EvaluationRun run, GenericImages spec)
    {
        if (!spec.BestEffort)
        {
            return ([], spec.Problem);
        }

        run.DropCauses.Add(spec.Problem + AskedAsText);
        return ([], null);
    }

    // More than 4 paths refuse a strict call; a best-effort one drops them all and asks as text: the judge never picks which 4, the caller does.
    private static (IReadOnlyList<string> Paths, string? Refusal) Capped(EvaluationRun run, GenericImages spec)
    {
        if (spec.Paths.Count <= GenericImages.MaxImages)
        {
            return (spec.Paths, null);
        }

        var cause = string.Create(CultureInfo.InvariantCulture, $"images has {spec.Paths.Count} paths; at most {GenericImages.MaxImages} are allowed");
        if (!spec.BestEffort)
        {
            return (spec.Paths, cause);
        }

        run.DropCauses.Add(cause + AskedAsText);
        return ([], null);
    }

    private async ValueTask<EvaluationOutcome> Refuse<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, GenericImages spec, string cause, CancellationToken cancellationToken)
        where TInput : class
    {
        run.Refusal = cause;
        run.Note = cause;
        if (run.Input is JsonObject input && spec.Paths.Count > 0)
        {
            input["images"] = new JsonArray([.. spec.Paths.Select(path => (JsonNode?)JsonValue.Create(path))]);
        }

        return await Escalate(question, run, EvaluationReasons.InvalidImages, DecisionOutcome.Escalated, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(ImageAttachment? Image, string Cause)> ReadOne(IImageReader reader, string path, int index, CancellationToken cancellationToken)
    {
        var label = string.Create(CultureInfo.InvariantCulture, $"images[{index}] {path}");
        if (!IsAbsolute(path))
        {
            return (null, $"{label} is not an absolute path");
        }

        var read = await reader.Read([path], root: null, cancellationToken).ConfigureAwait(false);
        if (read.Problem is { } problem)
        {
            var prefix = $"image {path} ";
            return (null, $"{label} {(problem.StartsWith(prefix, StringComparison.Ordinal) ? problem[prefix.Length..] : problem)}");
        }

        return (read.Images[0], "");
    }

    private static bool IsAbsolute(string path) =>
        path.StartsWith('/') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] is '/' or '\\'));

    // The row's input once the call had images: each kept image as {path, sha256, bytes, width, height} (never the bytes), and what was dropped on the
    // way. A call with no images, and a refused one (it keeps the paths as given), is stored exactly as before.
    private static JsonNode? WithImageRows(JsonNode? input, EvaluationRun run, string provider)
    {
        if (input is not JsonObject stored || (run.GenericRead.Count == 0 && run.DropCauses.Count == 0 && run.ProviderDrops.Count == 0))
        {
            return input;
        }

        var copy = (JsonObject)stored.DeepClone();
        if (run.GenericRead.Count > 0)
        {
            copy["images"] = new JsonArray([.. run.GenericRead.Select(Row)]);
        }

        var asked = string.Equals(provider, EvaluationReasons.NoProvider, StringComparison.Ordinal) ? "" : $"; asked {provider} {(run.GenericRead.Count == 0 ? "as text" : "without it")}";
        List<string> dropped = [.. run.DropCauses.Select(cause => cause.EndsWith(AskedAsText, StringComparison.Ordinal) ? cause : cause + asked), .. run.ProviderDrops];
        if (dropped.Count > 0)
        {
            copy["imagesDropped"] = new JsonArray([.. dropped.Select(line => (JsonNode?)JsonValue.Create(line))]);
        }

        return copy;
    }

    private static JsonNode? Row(ImageAttachment image) => new JsonObject
    {
        ["path"] = image.Path,
        ["sha256"] = image.Sha256,
        ["bytes"] = image.Bytes.Length,
        ["width"] = image.Width,
        ["height"] = image.Height,
    };
}
