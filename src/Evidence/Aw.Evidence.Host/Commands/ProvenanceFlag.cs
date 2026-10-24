using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Host.Commands;

/// <summary>Binds a 'seeded|unknown' flag value.</summary>
internal static class ProvenanceFlag
{
    /// <summary>Absent is fine (null); 'seeded' or 'unknown' bind; anything else is a usage error.</summary>
    internal static bool TryParse(string? text, out DbProvenance? provenance)
    {
        switch (text)
        {
            case null:
                provenance = null;
                return true;
            case "seeded":
                provenance = DbProvenance.Seeded;
                return true;
            case "unknown":
                provenance = DbProvenance.Unknown;
                return true;
            default:
                provenance = null;
                return false;
        }
    }

    /// <summary>Parity's pair of names for one setting: --provenance, or --db-provenance for one more release. Both at once is a usage error.</summary>
    internal static bool TryParseEither(CliArguments arguments, out DbProvenance? provenance)
    {
        var (current, old) = (arguments.Value("--provenance"), arguments.Value("--db-provenance"));
        if (current is not null && old is not null)
        {
            provenance = null;
            return false;
        }

        return TryParse(current ?? old, out provenance);
    }
}
