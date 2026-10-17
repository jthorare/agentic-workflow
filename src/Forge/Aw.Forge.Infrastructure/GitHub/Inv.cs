using System.Globalization;

namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>Formats an interpolated string with the invariant culture, so numbers in URLs and messages never depend on the machine's locale.</summary>
internal static class Inv
{
    internal static string S(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
