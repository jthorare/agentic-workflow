using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>The shape of a handoff (#2258 PR 12): a source name, an offer id and branch of sane size, a prompt of at most 65,536 characters, at most 50 links and 20 labels.</summary>
internal sealed partial class CreateHandoffItemValidator : IValidator<CreateHandoffItemRequest>
{
    internal const int MaxPrompt = 65_536;
    private const int MaxOffer = 128;
    private const int MaxBranch = 255;
    private const int MaxLinks = 50;
    private const int MaxLinkLength = 2048;
    private const int MaxLabels = 20;
    private const int MaxLabelLength = 64;

    public Result<Unit> Validate(CreateHandoffItemRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return (Problem(value) ?? Lists(value)) is { } problem ? SindriErrors.InvalidInput.With(problem) : Unit.Value;
    }

    private static string? Problem(CreateHandoffItemRequest value) => true switch
    {
        _ when value.Source is null || !SourceName.IsMatch(value.Source) => "the source must be 1 to 32 characters of a-z, 0-9, '.', '_' and '-' (fix: --source <name>)",
        _ when value.Offer is { } offer && (offer.Length is 0 or > MaxOffer || offer.Any(char.IsControl)) => $"the offer id is 1 to {MaxOffer} characters with no control character",
        _ when value.Branch is { } branch && (branch.Length is 0 or > MaxBranch || branch.Any(char.IsControl)) => $"the branch is 1 to {MaxBranch} characters with no control character",
        _ when value.Prompt is { Length: > MaxPrompt } prompt => $"the prompt is {prompt.Length} characters (fix: at most {MaxPrompt})",
        _ => null,
    };

    private static string? Lists(CreateHandoffItemRequest value) => true switch
    {
        _ when value.Links is { Count: > MaxLinks } => $"at most {MaxLinks} links",
        _ when value.Links is { } links && links.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > MaxLinkLength || l.Any(char.IsControl)) => $"each link is 1 to {MaxLinkLength} characters with no control character",
        _ when value.Labels is { Count: > MaxLabels } => $"at most {MaxLabels} labels",
        _ when value.Labels is { } labels && labels.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > MaxLabelLength || l.Any(char.IsControl)) => $"each label is 1 to {MaxLabelLength} characters with no control character",
        _ => null,
    };

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,31}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SourceName { get; }
}
