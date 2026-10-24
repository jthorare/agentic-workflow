using Aw.Judge.Core.Errors;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckTraits;

internal sealed class CheckTraitsValidator : IValidator<CheckTraitsRequest>
{
    public Result<Unit> Validate(CheckTraitsRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Problem(value) is { } problem ? JudgeErrors.Invalid.With(problem) : Unit.Value;
    }

    private static string? Problem(CheckTraitsRequest value) =>
        FilesMismatch(value) ? "--file goes with --edit, and --edit needs a file"
        : value.Mode != TraitCheckMode.Scan && (value.Paths is { Count: > 0 } || value.Traits is { Count: > 0 } || value.Concurrency is not null) ? "--path, --trait and --concurrency go with --scan"
        : NumberProblem(value);

    private static bool FilesMismatch(CheckTraitsRequest value)
    {
        var edit = value.Mode == TraitCheckMode.Edit;
        var files = value.Files.Count > 0;
        return edit != files;
    }

    private static string? NumberProblem(CheckTraitsRequest value) =>
        value.Budget is { } budget && budget <= TimeSpan.Zero ? "the budget must be positive"
        : value.MinProbability is { } minimum && minimum is not (>= 0 and <= 1) ? "--min-p must be from 0 to 1"
        : value.Concurrency is <= 0 ? "--concurrency must be positive"
        : null;
}
