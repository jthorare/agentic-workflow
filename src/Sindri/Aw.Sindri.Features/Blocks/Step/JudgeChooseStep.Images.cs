using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Step;

internal sealed partial class JudgeChooseStep
{
    private const int MaxImages = 4;

    // The optional 'images: <fact key>' (images spec 5.1): the fact is a list of absolute paths. The first four are the question's images; a path
    // that is not absolute, is missing, or is outside the places an image may come from (3.4) fails the Step before any judge call.
    private async ValueTask<(List<string> Images, int Dropped, string? Failure)> Images(StepContext context, CancellationToken cancellationToken)
    {
        if (Text(context, "images") is not { Length: > 0 } key)
        {
            return ([], 0, null);
        }

        if (StepFacts.Missing(context, [key]) is { Count: > 0 })
        {
            return ([], 0, $"{Name} needs {key}, which the instance does not hold");
        }

        if (StepFacts.Read(context, key) is not JsonArray list || list.Any(static e => e is not JsonValue v || !v.TryGetValue<string>(out _)))
        {
            return ([], 0, $"{Name}: images: {key} is not a list of file paths");
        }

        var paths = list.Select(static e => e!.GetValue<string>()).ToList();   // why: the check above held every entry to be a string
        var kept = paths.Take(MaxImages).ToList();
        var allowed = Roots is { } roots ? await roots.Of(context.Repo, cancellationToken).ConfigureAwait(false) : null;
        foreach (var path in kept)
        {
            if (Problem(path, allowed, Files) is { } problem)
            {
                return ([], 0, $"{Name}: images: {path} (from {key}) {problem}");
            }
        }

        return (kept, paths.Count - kept.Count, null);
    }

    // A caller with no file port (a test of something else) checks no file.
    private static string? Problem(string path, IReadOnlyList<string>? allowed, IStepImageFiles? files)
    {
        if (!Path.IsPathRooted(path))
        {
            return "is not an absolute path";
        }

        if (files is null)
        {
            return null;
        }

        // A '..' in the fact's own path is refused, as the hooks do: .NET folds it as text before opening, the OS follows it after a linked directory,
        // so which file it names depends on who reads it.
        if (allowed is not null && path.Split(['/', '\\']).Contains("..", StringComparer.Ordinal))
        {
            return "is outside the checkout and the ui-evidence output";
        }

        return files.RealPath(path) is not { } real ? "does not exist"
            : allowed is not null && !StepImagePlaces.Inside(real, allowed) ? "is outside the checkout and the ui-evidence output"
            : null;
    }
}
