using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.ReportDefect;

/// <summary>Checks the fields. The length limits are the scrub: a digest, a transcript or a code paste does not fit. A title holds no double quote: the duplicate search puts the title inside a quoted phrase (#2715).</summary>
internal sealed class ReportDefectValidator : IValidator<ReportDefectRequest>
{
    private const int TitleLimit = 120;
    private const int ErrorLimit = 400;
    private const int WhatLimit = 1500;
    private const int PathLimit = 200;

    public Result<Unit> Validate(ReportDefectRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var problem = Problem(value);
        return problem is null ? Unit.Value : MetaErrors.Invalid.With(problem);
    }

    private static string? Problem(ReportDefectRequest value)
    {
        if (string.IsNullOrWhiteSpace(value.Code) || string.IsNullOrWhiteSpace(value.Title) || string.IsNullOrWhiteSpace(value.What) || string.IsNullOrWhiteSpace(value.ErrorLine))
        {
            return "report-defect needs --code, --title, --what and --error";
        }

        return IsCode(value.Code.Trim())
            ? Lengths(value)
            : "--code looks like AW-META-010 or SND-SCRUB-002: capital letters and digits in groups joined by dashes";
    }

    private static string? Lengths(ReportDefectRequest value) =>
        Line(value.Title, TitleLimit, "--title")
        ?? (value.Title.Contains('"', StringComparison.Ordinal) ? "--title can't hold a double quote: the duplicate search puts the title inside one" : null)
        ?? Line(value.ErrorLine, ErrorLimit, "--error")
        ?? (value.What.Trim().Length > WhatLimit ? $"--what is at most {WhatLimit.ToString(CultureInfo.InvariantCulture)} characters" : null)
        ?? (value.Path is null ? null : Line(value.Path, PathLimit, "--path"));

    private static string? Line(string text, int limit, string name) =>
        text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal) ? $"{name} is one line"
        : text.Trim().Length > limit ? $"{name} is at most {limit.ToString(CultureInfo.InvariantCulture)} characters"
        : null;

    private static bool IsCode(string code)
    {
        var groups = code.Split('-');
        return groups.Length >= 2
            && groups.All(static g => g.Length > 0 && g.All(static c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            && groups[0][0] is >= 'A' and <= 'Z';
    }
}
