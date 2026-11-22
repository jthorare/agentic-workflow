using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>Splits a name into its parts at case and letter-digit boundaries ('AcmeCare', 'GLXCare', 'acme2'), lowercased.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "A stub until the next commit, where the tier rules and leak layers call it.")]
internal static class NameParts
{
    /// <summary>The parts of one word of letters and digits.</summary>
    /// <param name="word">The word as written.</param>
    /// <returns>The lowercased parts, in order.</returns>
    public static IReadOnlyList<string> Split(string word) => throw new NotSupportedException();
}
