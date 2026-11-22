using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.Delegate;

/// <summary>Checks the fields, with report-defect's limits: a title of one line and at most 120 characters, a text of at most 1500.</summary>
internal sealed class DelegateValidator : IValidator<DelegateRequest>
{
    private const int TitleLimit = 120;
    private const int WhatLimit = 1500;
    private const int LabelLimit = 50;

    public Result<Unit> Validate(DelegateRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var problem = Problem(value);
        return problem is null ? Unit.Value : MetaErrors.Invalid.With(problem);
    }

    private static string? Problem(DelegateRequest value)
    {
        if (value.Issue is < 1)
        {
            return "--issue is an issue number, 1 or more";
        }

        if (value.Issue is null && (string.IsNullOrWhiteSpace(value.Title) || string.IsNullOrWhiteSpace(value.What)))
        {
            return "delegate needs --title and --what (or --issue <n> to delegate an existing issue)";
        }

        return Fields(value);
    }

    private static string? Fields(DelegateRequest value) =>
        (value.Title is null ? null : Line(value.Title, TitleLimit, "--title"))
        ?? (value.What is not null && value.What.Trim().Length > WhatLimit ? $"--what is at most {WhatLimit.ToString(CultureInfo.InvariantCulture)} characters" : null)
        ?? value.Labels.Select(static l => string.IsNullOrWhiteSpace(l) ? "--label must not be blank" : Line(l, LabelLimit, "--label")).FirstOrDefault(static p => p is not null);

    private static string? Line(string text, int limit, string name) =>
        text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal) ? $"{name} is one line"
        : text.Trim().Length > limit ? $"{name} is at most {limit.ToString(CultureInfo.InvariantCulture)} characters"
        : null;
}
