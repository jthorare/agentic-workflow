using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// The guard on a SQLite seed: the seed runs on the run's own copy only, so a statement that names or opens another file is refused with
/// the same word list that keeps a query inside the copy (ATTACH, DETACH, VACUUM, which can write to a path) plus load_extension.
/// </summary>
public static class SeedStatements
{
    private static readonly string[] _reachingWords = ["ATTACH", "DETACH", "VACUUM", "LOAD_EXTENSION"];

    /// <summary>Checks a seed's text.</summary>
    /// <param name="sql">The seed SQL (any number of statements).</param>
    /// <returns>Success, or AW-EVIDENCE-020 naming the word that would reach beyond the copy.</returns>
    public static Result<Unit> Check(string sql)
    {
        foreach (var statement in SqlText.Statements(sql, nestedComments: false))
        {
            if (SqlText.Words(statement).FirstOrDefault(word => _reachingWords.Contains(word, StringComparer.Ordinal)) is { } word)
            {
                return EvidenceErrors.DatabaseRefused.With($"the seed uses {word}, which would reach beyond the run's copy: a seed runs on the copy only and may not attach, detach, vacuum or load anything");
            }
        }

        return Unit.Value;
    }
}
