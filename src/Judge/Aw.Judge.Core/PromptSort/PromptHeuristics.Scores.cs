namespace Aw.Judge.Core.PromptSort;

// The two score axes: additive complexity and ambiguity credits (after Navigator's scoring.py).
internal static partial class PromptHeuristics
{
    private const double BaseScore = 0.5;
    private const double VagueBonus = 0.2;
    private const double CreditFile = 0.4;
    private const double CreditNumber = 0.2;
    private const double CreditLimiter = 0.2;
    private const double CreditAcceptance = 0.3;

    private static readonly string[] _high = ["refactor", "implement", "add feature", "new feature", "architecture", "redesign", "migrate", "overhaul"];
    private static readonly string[] _medium = ["fix all", "update all", "change all", "modify", "enhance", "improve", "extend", "integrate"];
    private static readonly string[] _low = ["add", "create", "update", "fix", "change", "remove", "delete"];
    private static readonly string[] _multiFile = ["multiple files", "several files", "across", "all files", "everywhere", "throughout", "project-wide", "codebase"];

    private static string ComplexityLevel(PhraseText text, bool task)
    {
        if (!task)
        {
            return "trivial";
        }

        var score = (text.Count(_high) * 0.3) + (text.Count(_medium) * 0.2) + (text.Count(_low) * 0.1) + (text.HasAny(_multiFile) ? 0.2 : 0);
        var rounded = Math.Round(Math.Min(score, 1) * 100, MidpointRounding.AwayFromZero) / 100;
        return rounded switch
        {
            < 0.2 => "trivial",
            < 0.5 => "small",
            < 0.8 => "substantial",
            _ => "large",
        };
    }

    private static string AmbiguityLevel(string raw, PhraseText text, bool task)
    {
        if (!task)
        {
            return "clear";
        }

        var score = BaseScore;
        score += text.HasAny(_vagueScope) ? VagueBonus : 0;
        score -= PathLike.IsMatch(raw) || FileName.IsMatch(raw) ? CreditFile : 0;
        score -= Number.IsMatch(text.Text) ? CreditNumber : 0;
        score -= text.HasAny(_limiters) ? CreditLimiter : 0;
        score -= text.HasAny(_acceptance) ? CreditAcceptance : 0;
        var rounded = Math.Round(Math.Max(0, Math.Min(1, score)) * 100, MidpointRounding.AwayFromZero) / 100;
        return rounded switch
        {
            >= 0.5 => "vague",
            >= 0.2 => "partly",
            _ => "clear",
        };
    }
}
