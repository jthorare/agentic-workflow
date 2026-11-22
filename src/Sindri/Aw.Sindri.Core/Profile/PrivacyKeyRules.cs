using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks on 'privacy': no blank term, and every deny pattern compiles (a pattern that doesn't would hold every proposal at the gate).</summary>
public static class PrivacyKeyRules
{
    /// <summary>Checks the 'privacy' section.</summary>
    /// <param name="privacy">The section, or null.</param>
    /// <returns>SND-PROFILE-002 with the key path, or null.</returns>
    public static Error? Check(PrivacySettings? privacy)
    {
        if (privacy is null)
        {
            return null;
        }

        var terms = privacy.DenyTerms ?? [];
        for (var i = 0; i < terms.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(terms[i]))
            {
                return SindriErrors.ProfileInvalid.With(string.Create(CultureInfo.InvariantCulture, $"privacy.denyTerms[{i}]: is blank"));
            }
        }

        var patterns = privacy.DenyPatterns ?? [];
        for (var i = 0; i < patterns.Count; i++)
        {
            if (!Compiles(patterns[i]))
            {
                return SindriErrors.ProfileInvalid.With(string.Create(CultureInfo.InvariantCulture, $"privacy.denyPatterns[{i}]: is not a regular expression .NET can compile"));
            }
        }

        return null;
    }

    // why: the error never repeats the pattern; it may itself be private
    private static bool Compiles(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
