using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Transcripts.Core.Turns;

/// <summary>
/// The dedupe key of a turn: the SHA-256, in hex, of its role, its start time, its whitespace-normalized text and its
/// tools. A resumed or forked session copies earlier lines into a new file with the same timestamps, so a copy has the
/// original's key. The key is a digest so a scan keeps 64 characters per turn, not the turn's text.
/// </summary>
public static class TurnKey
{
    /// <summary>The key of a turn.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The key; two turns with the same key are one turn.</returns>
    public static string Of(TranscriptTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var key = new StringBuilder();
        _ = key.Append(turn.Role == TurnRole.Human ? 'h' : 'a').Append('\0')
            .Append(turn.At.UtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\0');
        var space = false;
        foreach (var c in turn.Text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                _ = key.Append(' ');
                space = false;
            }

            _ = key.Append(c);
        }

        var bytes = Encoding.UTF8.GetBytes(key.Append('\0').AppendJoin(',', turn.Tools).ToString());
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
