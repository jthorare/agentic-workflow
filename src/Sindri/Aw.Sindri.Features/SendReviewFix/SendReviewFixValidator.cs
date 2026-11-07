using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>The shape of a fix task (PR review page spec §8.2): a known kind, 1 to 2,000 characters of text, a 40-digit head, a line with its file, a known side.</summary>
internal sealed partial class SendReviewFixValidator : IValidator<SendReviewFixRequest>
{
    internal const int MaxText = 2000;

    private static readonly string[] _kinds = ["check", "requirement", "line"];

    public Result<Unit> Validate(SendReviewFixRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Problem(value) is { } problem ? SindriErrors.InvalidInput.With(problem) : Unit.Value;
    }

    private static string? Problem(SendReviewFixRequest value) =>
        !_kinds.Contains(value.Kind, StringComparer.Ordinal) ? "the kind is check, requirement or line"
        : string.IsNullOrWhiteSpace(value.Text) || value.Text.Length > MaxText ? $"the text is 1 to {MaxText} characters"
        : value.Head is not { } head || !HeadShape.IsMatch(head) ? "the head is 40 hex digits"
        : value.Number < 1 ? "the PR number is 1 or more"
        : PlaceProblem(value);

    private static string? PlaceProblem(SendReviewFixRequest value) =>
        value.Side is not (null or "new" or "old") ? "the side is new or old"
        : value.Line is { } line && (line < 1 || string.IsNullOrEmpty(value.Path)) ? "a line needs its file and a number of 1 or more"
        : null;

    [GeneratedRegex("^[0-9a-fA-F]{40}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeadShape { get; }
}
